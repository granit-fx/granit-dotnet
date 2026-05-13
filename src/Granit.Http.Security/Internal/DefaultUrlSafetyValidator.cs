using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Granit.Http.Security.Diagnostics;
using Granit.Http.Security.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.Security.Internal;

/// <summary>
/// Default <see cref="IUrlSafetyValidator"/>. See package docs for the order of checks.
/// </summary>
internal sealed class DefaultUrlSafetyValidator : IUrlSafetyValidator
{
    private static readonly IdnMapping IdnMapping = new();

    private readonly IOptions<UrlSafetyOptions> _options;
    private readonly IDnsResolver _resolver;
    private readonly TimeProvider _timeProvider;
    private readonly HttpSecurityMetrics _metrics;
    private readonly ICurrentTenant? _currentTenant;
    private readonly ILogger<DefaultUrlSafetyValidator> _logger;

    public DefaultUrlSafetyValidator(
        IOptions<UrlSafetyOptions> options,
        IDnsResolver resolver,
        TimeProvider timeProvider,
        HttpSecurityMetrics metrics,
        ILogger<DefaultUrlSafetyValidator> logger,
        ICurrentTenant? currentTenant = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _resolver = resolver;
        _timeProvider = timeProvider;
        _metrics = metrics;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public ValueTask<UrlSafetyResult> ValidateAsync(Uri url, CancellationToken ct = default) =>
        ValidateAsync(url, _options.Value, ct);

    public async ValueTask<UrlSafetyResult> ValidateAsync(Uri url, UrlSafetyOptions optionOverrides, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(optionOverrides);

        using Activity? activity = HttpSecurityActivitySource.Source.StartActivity(HttpSecurityActivitySource.ValidateUrl);

        // (1) Absolute-URI gate.
        if (!url.IsAbsoluteUri)
        {
            return Block(activity, new UrlSafetyViolation(
                UrlSafetyViolationKind.MalformedUrl,
                "URL is not absolute.",
                "UrlSafety:MalformedUrl",
                []));
        }

        // (2) Length cap.
        if (url.OriginalString.Length > optionOverrides.MaxUrlLength)
        {
            return Block(activity, new UrlSafetyViolation(
                UrlSafetyViolationKind.UrlTooLong,
                $"URL exceeds the maximum length of {optionOverrides.MaxUrlLength}.",
                "UrlSafety:UrlTooLong",
                [optionOverrides.MaxUrlLength]));
        }

        // (3) Scheme allowlist.
        if (!IsSchemeAllowed(url.Scheme, optionOverrides.AllowedSchemes))
        {
            return Block(activity, new UrlSafetyViolation(
                UrlSafetyViolationKind.SchemeNotAllowed,
                $"URL scheme '{url.Scheme}' is not allowed.",
                "UrlSafety:SchemeNotAllowed",
                [url.Scheme]));
        }

        // (4) file:// — gated by AllowFileScheme AND empty authority (no UNC).
        if (string.Equals(url.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        {
            if (!optionOverrides.AllowFileScheme)
            {
                return Block(activity, new UrlSafetyViolation(
                    UrlSafetyViolationKind.SchemeNotAllowed,
                    "The file scheme is disabled. Set UrlSafetyOptions.AllowFileScheme=true to opt in.",
                    "UrlSafety:SchemeNotAllowed",
                    [url.Scheme]));
            }

            if (!string.IsNullOrEmpty(url.Host))
            {
                // file://server/share — UNC path, triggers SMB egress and NTLM-relay on Windows.
                return Block(activity, new UrlSafetyViolation(
                    UrlSafetyViolationKind.SchemeNotAllowed,
                    "UNC file:// paths are not allowed.",
                    "UrlSafety:SchemeNotAllowed",
                    [url.Scheme]));
            }

            RecordValid();
            activity?.SetTag("url_safety.outcome", "valid");
            return UrlSafetyResult.Valid([]);
        }

        string host = url.Host;
        if (string.IsNullOrEmpty(host))
        {
            return Block(activity, new UrlSafetyViolation(
                UrlSafetyViolationKind.MalformedUrl,
                "URL has no host.",
                "UrlSafety:MalformedUrl",
                []));
        }

        // (5) IDN normalization → punycode (skip when host is an IP literal or AllowIdn is off).
        string asciiHost = optionOverrides.AllowIdn ? NormalizeHost(host) : host;

        // (6) Reserved TLD check (skipped for IP literals).
        if (!IPAddress.TryParse(StripBrackets(asciiHost), out IPAddress? literalIp)
            && ReservedTldClassifier.IsReserved(asciiHost, out string? tld))
        {
            return Block(activity, new UrlSafetyViolation(
                UrlSafetyViolationKind.ReservedTld,
                $"Host '{SanitizeForDisplay(asciiHost)}' uses the reserved TLD '{tld}'.",
                "UrlSafety:ReservedTld",
                [asciiHost, tld]));
        }

        // (7) Deny patterns.
        if (optionOverrides.DeniedHostPatterns.Count > 0 && HostPatternMatcher.MatchesAny(asciiHost, optionOverrides.DeniedHostPatterns))
        {
            return Block(activity, new UrlSafetyViolation(
                UrlSafetyViolationKind.HostPatternDenied,
                $"Host '{SanitizeForDisplay(asciiHost)}' matches a denied pattern.",
                "UrlSafety:HostPatternDenied",
                [asciiHost]));
        }

        // (8) Allow patterns (if non-empty, must match at least one).
        if (optionOverrides.AllowedHostPatterns.Count > 0 && !HostPatternMatcher.MatchesAny(asciiHost, optionOverrides.AllowedHostPatterns))
        {
            return Block(activity, new UrlSafetyViolation(
                UrlSafetyViolationKind.HostPatternNotAllowed,
                $"Host '{SanitizeForDisplay(asciiHost)}' is not in the allowed pattern list.",
                "UrlSafety:HostPatternNotAllowed",
                [asciiHost]));
        }

        // (9 / 10) Resolve and classify. IP literal → classify directly.
        IPAddress[] addresses;
        if (literalIp is not null)
        {
            addresses = [literalIp];
        }
        else
        {
            try
            {
                using var cts = new CancellationTokenSource(optionOverrides.DnsResolveTimeout, _timeProvider);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);
                addresses = await _resolver.GetHostAddressesAsync(asciiHost, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                HttpSecurityLog.DnsResolutionFailed(_logger, SanitizeForDisplay(asciiHost), "timeout");
                return Block(activity, new UrlSafetyViolation(
                    UrlSafetyViolationKind.DnsResolutionFailed,
                    $"DNS resolution timed out for host '{SanitizeForDisplay(asciiHost)}'.",
                    "UrlSafety:DnsResolutionFailed",
                    [asciiHost]));
            }
            catch (SocketException)
            {
                HttpSecurityLog.DnsResolutionFailed(_logger, SanitizeForDisplay(asciiHost), "socket_error");
                return Block(activity, new UrlSafetyViolation(
                    UrlSafetyViolationKind.DnsResolutionFailed,
                    $"DNS resolution failed for host '{SanitizeForDisplay(asciiHost)}'.",
                    "UrlSafety:DnsResolutionFailed",
                    [asciiHost]));
            }

            if (addresses.Length == 0)
            {
                HttpSecurityLog.DnsResolutionFailed(_logger, SanitizeForDisplay(asciiHost), "no_addresses");
                return Block(activity, new UrlSafetyViolation(
                    UrlSafetyViolationKind.DnsResolutionFailed,
                    $"DNS resolution returned no addresses for host '{SanitizeForDisplay(asciiHost)}'.",
                    "UrlSafety:DnsResolutionFailed",
                    [asciiHost]));
            }
        }

        foreach (IPAddress ip in addresses)
        {
            if (PrivateNetworkClassifier.Classify(ip, out UrlSafetyViolationKind kind))
            {
                if (IsAllowedByPolicy(kind, optionOverrides))
                {
                    continue;
                }

                return Block(activity, new UrlSafetyViolation(
                    kind,
                    BuildReason(kind, SanitizeForDisplay(asciiHost)),
                    LocalizationKeyFor(kind),
                    [asciiHost]));
            }
        }

        RecordValid();
        activity?.SetTag("url_safety.outcome", "valid");
        return UrlSafetyResult.Valid(addresses);
    }

    private string? CurrentTenantId() =>
        _currentTenant is { IsAvailable: true, Id: { } id } ? id.ToString() : null;

    private void RecordValid() => _metrics.RecordValid(CurrentTenantId());

    private UrlSafetyResult Block(Activity? activity, UrlSafetyViolation violation)
    {
        _metrics.RecordBlocked(CurrentTenantId(), violation.Kind);
        HttpSecurityLog.UrlBlocked(_logger, SanitizeForDisplay(ExtractHost(violation)), violation.Kind, violation.Reason);
        activity?.SetTag("url_safety.outcome", "blocked");
        activity?.SetTag("url_safety.violation_kind", violation.Kind.ToString());
        return UrlSafetyResult.Invalid(violation);
    }

    private static string ExtractHost(UrlSafetyViolation violation) =>
        violation.Args.Count > 0 && violation.Args[0] is string h ? h : "<unknown>";

    private static bool IsSchemeAllowed(string scheme, IReadOnlyList<string> allowed) =>
        allowed.Any(s => string.Equals(scheme, s, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeHost(string host)
    {
        // Uri.Host strips IPv6 brackets, but defend against callers building Uri-like strings by hand.
        string unbracketed = StripBrackets(host);

        if (IPAddress.TryParse(unbracketed, out _))
        {
            return unbracketed;
        }

        try
        {
            return IdnMapping.GetAscii(unbracketed);
        }
        catch (ArgumentException)
        {
            return unbracketed;
        }
    }

    private static string StripBrackets(string host) =>
        host.Length >= 2 && host[0] == '[' && host[^1] == ']' ? host[1..^1] : host;

    /// <summary>
    /// Strips Unicode control / format code points (e.g. U+202E RTL override) that could spoof
    /// log entries. Replaces them with <c>?</c>.
    /// </summary>
    private static string SanitizeForDisplay(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        StringBuilder? sb = null;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            bool unsafeChar = char.IsControl(c)
                || cat == UnicodeCategory.Format
                || cat == UnicodeCategory.LineSeparator
                || cat == UnicodeCategory.ParagraphSeparator;
            if (unsafeChar)
            {
                sb ??= new StringBuilder(value.Length).Append(value, 0, i);
                sb.Append('?');
            }
            else
            {
                sb?.Append(c);
            }
        }

        return sb?.ToString() ?? value;
    }

    private static bool IsAllowedByPolicy(UrlSafetyViolationKind kind, UrlSafetyOptions opts) =>
        kind switch
        {
            UrlSafetyViolationKind.Loopback => opts.AllowLoopback,
            UrlSafetyViolationKind.PrivateNetwork => opts.AllowPrivateNetworks,
            _ => false,
        };

    private static string BuildReason(UrlSafetyViolationKind kind, string host) => kind switch
    {
        UrlSafetyViolationKind.Loopback => $"Host '{host}' resolves to a loopback address.",
        UrlSafetyViolationKind.PrivateNetwork => $"Host '{host}' resolves to a private network address.",
        UrlSafetyViolationKind.LinkLocal => $"Host '{host}' resolves to a link-local address.",
        UrlSafetyViolationKind.MetadataEndpoint => $"Host '{host}' resolves to a cloud metadata endpoint.",
        UrlSafetyViolationKind.IPv6UniqueLocal => $"Host '{host}' resolves to an IPv6 unique-local address.",
        UrlSafetyViolationKind.ReservedAddress => $"Host '{host}' resolves to a reserved (multicast / broadcast / future-use) address.",
        UrlSafetyViolationKind.IPv6EmbeddedIPv4 => $"Host '{host}' resolves to an IPv6 transition address whose embedded IPv4 is sensitive.",
        _ => $"Host '{host}' is blocked.",
    };

    private static string LocalizationKeyFor(UrlSafetyViolationKind kind) => kind switch
    {
        UrlSafetyViolationKind.Loopback => "UrlSafety:Loopback",
        UrlSafetyViolationKind.PrivateNetwork => "UrlSafety:PrivateNetwork",
        UrlSafetyViolationKind.LinkLocal => "UrlSafety:LinkLocal",
        UrlSafetyViolationKind.MetadataEndpoint => "UrlSafety:MetadataEndpoint",
        UrlSafetyViolationKind.IPv6UniqueLocal => "UrlSafety:IPv6UniqueLocal",
        UrlSafetyViolationKind.ReservedAddress => "UrlSafety:ReservedAddress",
        UrlSafetyViolationKind.IPv6EmbeddedIPv4 => "UrlSafety:IPv6EmbeddedIPv4",
        _ => "UrlSafety:HostBlocked",
    };
}
