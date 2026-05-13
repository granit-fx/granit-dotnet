using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Granit.Http.Security.Diagnostics;
using Granit.Http.Security.Options;
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
    private readonly ILogger<DefaultUrlSafetyValidator> _logger;

    public DefaultUrlSafetyValidator(
        IOptions<UrlSafetyOptions> options,
        IDnsResolver resolver,
        TimeProvider timeProvider,
        HttpSecurityMetrics metrics,
        ILogger<DefaultUrlSafetyValidator> logger)
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

        // (4) file:// short-circuit — no DNS / no IP classification.
        // Note: enabling "file" in AllowedSchemes also lets through UNC paths on Windows
        // (file://server/share) — only opt in for trusted, local-only contexts.
        if (string.Equals(url.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        {
            _metrics.RecordValid(tenantId: null);
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
                $"Host '{asciiHost}' uses the reserved TLD '{tld}'.",
                "UrlSafety:ReservedTld",
                [asciiHost, tld]));
        }

        // (7) Deny patterns.
        if (optionOverrides.DeniedHostPatterns.Count > 0 && HostPatternMatcher.MatchesAny(asciiHost, optionOverrides.DeniedHostPatterns))
        {
            return Block(activity, new UrlSafetyViolation(
                UrlSafetyViolationKind.HostPatternDenied,
                $"Host '{asciiHost}' matches a denied pattern.",
                "UrlSafety:HostPatternDenied",
                [asciiHost]));
        }

        // (8) Allow patterns (if non-empty, must match at least one).
        if (optionOverrides.AllowedHostPatterns.Count > 0 && !HostPatternMatcher.MatchesAny(asciiHost, optionOverrides.AllowedHostPatterns))
        {
            return Block(activity, new UrlSafetyViolation(
                UrlSafetyViolationKind.HostPatternNotAllowed,
                $"Host '{asciiHost}' is not in the allowed pattern list.",
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
                HttpSecurityLog.DnsResolutionFailed(_logger, asciiHost, "timeout");
                return Block(activity, new UrlSafetyViolation(
                    UrlSafetyViolationKind.DnsResolutionFailed,
                    $"DNS resolution timed out for host '{asciiHost}'.",
                    "UrlSafety:DnsResolutionFailed",
                    [asciiHost]));
            }
            catch (SocketException)
            {
                HttpSecurityLog.DnsResolutionFailed(_logger, asciiHost, "socket_error");
                return Block(activity, new UrlSafetyViolation(
                    UrlSafetyViolationKind.DnsResolutionFailed,
                    $"DNS resolution failed for host '{asciiHost}'.",
                    "UrlSafety:DnsResolutionFailed",
                    [asciiHost]));
            }

            if (addresses.Length == 0)
            {
                HttpSecurityLog.DnsResolutionFailed(_logger, asciiHost, "no_addresses");
                return Block(activity, new UrlSafetyViolation(
                    UrlSafetyViolationKind.DnsResolutionFailed,
                    $"DNS resolution returned no addresses for host '{asciiHost}'.",
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
                    BuildReason(kind, asciiHost),
                    LocalizationKeyFor(kind),
                    [asciiHost]));
            }
        }

        _metrics.RecordValid(tenantId: null);
        activity?.SetTag("url_safety.outcome", "valid");
        return UrlSafetyResult.Valid(addresses);
    }

    private UrlSafetyResult Block(Activity? activity, UrlSafetyViolation violation)
    {
        _metrics.RecordBlocked(tenantId: null, violation.Kind);
        HttpSecurityLog.UrlBlocked(_logger, ExtractHost(violation), violation.Kind, violation.Reason);
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
        _ => $"Host '{host}' is blocked.",
    };

    private static string LocalizationKeyFor(UrlSafetyViolationKind kind) => kind switch
    {
        UrlSafetyViolationKind.Loopback => "UrlSafety:Loopback",
        UrlSafetyViolationKind.PrivateNetwork => "UrlSafety:PrivateNetwork",
        UrlSafetyViolationKind.LinkLocal => "UrlSafety:LinkLocal",
        UrlSafetyViolationKind.MetadataEndpoint => "UrlSafety:MetadataEndpoint",
        UrlSafetyViolationKind.IPv6UniqueLocal => "UrlSafety:IPv6UniqueLocal",
        _ => "UrlSafety:HostBlocked",
    };
}
