using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Granit.Diagnostics;
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

        // (1)(2)(3) Cheap structural gates.
        if (ValidateUrlShape(url, optionOverrides) is { } shapeViolation)
        {
            return Block(activity, shapeViolation);
        }

        // (4) file:// — handled separately because it short-circuits host resolution.
        if (string.Equals(url.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        {
            return HandleFileScheme(activity, url, optionOverrides);
        }

        // (5)(6)(7)(8) Host normalization + reserved/deny/allow checks.
        if (ValidateHost(url.Host, optionOverrides, out string asciiHost, out IPAddress? literalIp) is { } hostViolation)
        {
            return Block(activity, hostViolation);
        }

        // (9) Resolve addresses (IP literal short-circuits DNS).
        (IPAddress[]? addresses, UrlSafetyViolation? resolveViolation) =
            await ResolveAddressesAsync(asciiHost, literalIp, optionOverrides, ct).ConfigureAwait(false);
        if (resolveViolation is not null)
        {
            return Block(activity, resolveViolation);
        }

        // (10) Classify each resolved address against the private-network policy.
        if (ClassifyAddresses(addresses!, asciiHost, optionOverrides) is { } classifyViolation)
        {
            return Block(activity, classifyViolation);
        }

        RecordValid();
        activity?.SetTag("url_safety.outcome", "valid");
        return UrlSafetyResult.Valid(addresses!);
    }

    private static UrlSafetyViolation? ValidateUrlShape(Uri url, UrlSafetyOptions opts)
    {
        if (!url.IsAbsoluteUri)
        {
            return new UrlSafetyViolation(
                UrlSafetyViolationKind.MalformedUrl,
                "URL is not absolute.",
                "UrlSafety:MalformedUrl",
                []);
        }

        if (url.OriginalString.Length > opts.MaxUrlLength)
        {
            return new UrlSafetyViolation(
                UrlSafetyViolationKind.UrlTooLong,
                $"URL exceeds the maximum length of {opts.MaxUrlLength}.",
                "UrlSafety:UrlTooLong",
                [opts.MaxUrlLength]);
        }

        if (!IsSchemeAllowed(url.Scheme, opts.AllowedSchemes))
        {
            return new UrlSafetyViolation(
                UrlSafetyViolationKind.SchemeNotAllowed,
                $"URL scheme '{url.Scheme}' is not allowed.",
                "UrlSafety:SchemeNotAllowed",
                [url.Scheme]);
        }

        return null;
    }

    private UrlSafetyResult HandleFileScheme(Activity? activity, Uri url, UrlSafetyOptions opts)
    {
        if (!opts.AllowFileScheme)
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

    private static UrlSafetyViolation? ValidateHost(
        string host,
        UrlSafetyOptions opts,
        out string asciiHost,
        out IPAddress? literalIp)
    {
        asciiHost = string.Empty;
        literalIp = null;

        if (string.IsNullOrEmpty(host))
        {
            return new UrlSafetyViolation(
                UrlSafetyViolationKind.MalformedUrl,
                "URL has no host.",
                "UrlSafety:MalformedUrl",
                []);
        }

        asciiHost = opts.AllowIdn ? NormalizeHost(host) : host;

        bool isIpLiteral = IPAddress.TryParse(StripBrackets(asciiHost), out literalIp);

        if (!isIpLiteral && ReservedTldClassifier.IsReserved(asciiHost, out string? tld))
        {
            return new UrlSafetyViolation(
                UrlSafetyViolationKind.ReservedTld,
                $"Host '{SanitizeForDisplay(asciiHost)}' uses the reserved TLD '{tld}'.",
                "UrlSafety:ReservedTld",
                [asciiHost, tld]);
        }

        if (opts.DeniedHostPatterns.Count > 0 && HostPatternMatcher.MatchesAny(asciiHost, opts.DeniedHostPatterns))
        {
            return new UrlSafetyViolation(
                UrlSafetyViolationKind.HostPatternDenied,
                $"Host '{SanitizeForDisplay(asciiHost)}' matches a denied pattern.",
                "UrlSafety:HostPatternDenied",
                [asciiHost]);
        }

        if (opts.AllowedHostPatterns.Count > 0 && !HostPatternMatcher.MatchesAny(asciiHost, opts.AllowedHostPatterns))
        {
            return new UrlSafetyViolation(
                UrlSafetyViolationKind.HostPatternNotAllowed,
                $"Host '{SanitizeForDisplay(asciiHost)}' is not in the allowed pattern list.",
                "UrlSafety:HostPatternNotAllowed",
                [asciiHost]);
        }

        return null;
    }

    private async ValueTask<(IPAddress[]? Addresses, UrlSafetyViolation? Violation)> ResolveAddressesAsync(
        string asciiHost,
        IPAddress? literalIp,
        UrlSafetyOptions opts,
        CancellationToken ct)
    {
        if (literalIp is not null)
        {
            return ([literalIp], null);
        }

        IPAddress[] addresses;
        try
        {
            using var cts = new CancellationTokenSource(opts.DnsResolveTimeout, _timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);
            addresses = await _resolver.GetHostAddressesAsync(asciiHost, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (null, DnsFailure(asciiHost, "timeout", "DNS resolution timed out for host"));
        }
        catch (SocketException)
        {
            return (null, DnsFailure(asciiHost, "socket_error", "DNS resolution failed for host"));
        }

        if (addresses.Length == 0)
        {
            return (null, DnsFailure(asciiHost, "no_addresses", "DNS resolution returned no addresses for host"));
        }

        return (addresses, null);
    }

    private UrlSafetyViolation DnsFailure(string asciiHost, string failureTag, string reasonPrefix)
    {
        HttpSecurityLog.DnsResolutionFailed(_logger, SanitizeForDisplay(asciiHost), failureTag);
        return new UrlSafetyViolation(
            UrlSafetyViolationKind.DnsResolutionFailed,
            $"{reasonPrefix} '{SanitizeForDisplay(asciiHost)}'.",
            "UrlSafety:DnsResolutionFailed",
            [asciiHost]);
    }

    private static UrlSafetyViolation? ClassifyAddresses(IPAddress[] addresses, string asciiHost, UrlSafetyOptions opts)
    {
        foreach (IPAddress ip in addresses)
        {
            if (!PrivateNetworkClassifier.Classify(ip, out UrlSafetyViolationKind kind))
            {
                continue;
            }

            if (IsAllowedByPolicy(kind, opts))
            {
                continue;
            }

            return new UrlSafetyViolation(
                kind,
                BuildReason(kind, SanitizeForDisplay(asciiHost)),
                LocalizationKeyFor(kind),
                [asciiHost]);
        }

        return null;
    }

    private string? CurrentTenantId() =>
        _currentTenant is { IsAvailable: true, Id: { } id } ? id.ToString() : null;

    private void RecordValid() => _metrics.RecordValid(CurrentTenantId());

    private UrlSafetyResult Block(Activity? activity, UrlSafetyViolation violation)
    {
        _metrics.RecordBlocked(CurrentTenantId(), violation.Kind);
        HttpSecurityLog.UrlBlocked(_logger, RedactHostForLog(ExtractHost(violation)), violation.Kind, violation.Reason);
        activity?.SetTag("url_safety.outcome", "blocked");
        activity?.SetTag("url_safety.violation_kind", violation.Kind.ToString());
        return UrlSafetyResult.Invalid(violation);
    }

    private static string ExtractHost(UrlSafetyViolation violation) =>
        violation.Args.Count > 0 && violation.Args[0] is string h ? h : "<unknown>";

    /// <summary>
    /// IP-literal targets are redacted (GDPR — an IP can be personal data) on top of the
    /// control-character sanitization applied to every logged host.
    /// </summary>
    private static string RedactHostForLog(string host) =>
        IPAddress.TryParse(StripBrackets(host), out _)
            ? LogRedaction.IpAddress(StripBrackets(host))
            : SanitizeForDisplay(host);

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
