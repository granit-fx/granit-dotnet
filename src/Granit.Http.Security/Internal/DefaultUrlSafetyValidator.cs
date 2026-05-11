using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Granit.Http.Security.Options;
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

    public DefaultUrlSafetyValidator(
        IOptions<UrlSafetyOptions> options,
        IDnsResolver resolver,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _options = options;
        _resolver = resolver;
        _timeProvider = timeProvider;
    }

    public ValueTask<UrlSafetyResult> ValidateAsync(Uri url, CancellationToken ct = default) =>
        ValidateAsync(url, _options.Value, ct);

    public async ValueTask<UrlSafetyResult> ValidateAsync(Uri url, UrlSafetyOptions optionOverrides, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(optionOverrides);

        // (1) Absolute-URI gate.
        if (!url.IsAbsoluteUri)
        {
            return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                UrlSafetyViolationKind.MalformedUrl,
                "URL is not absolute.",
                "UrlSafety:MalformedUrl"));
        }

        // (2) Length cap.
        if (url.OriginalString.Length > optionOverrides.MaxUrlLength)
        {
            return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                UrlSafetyViolationKind.UrlTooLong,
                $"URL exceeds the maximum length of {optionOverrides.MaxUrlLength}.",
                "UrlSafety:UrlTooLong"));
        }

        // (3) Scheme allowlist.
        if (!IsSchemeAllowed(url.Scheme, optionOverrides.AllowedSchemes))
        {
            return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                UrlSafetyViolationKind.SchemeNotAllowed,
                $"URL scheme '{url.Scheme}' is not allowed.",
                "UrlSafety:SchemeNotAllowed"));
        }

        // (4) file:// short-circuit — no DNS / no IP classification.
        if (string.Equals(url.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        {
            return UrlSafetyResult.Valid([]);
        }

        string host = url.Host;
        if (string.IsNullOrEmpty(host))
        {
            return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                UrlSafetyViolationKind.MalformedUrl,
                "URL has no host.",
                "UrlSafety:MalformedUrl"));
        }

        // (5) IDN normalization → punycode (skip when host is a bracketed IPv6 literal).
        string asciiHost = NormalizeHost(host);

        // (6) Reserved TLD check (skipped for IP literals).
        if (!IPAddress.TryParse(host, out IPAddress? literalIp) && ReservedTldClassifier.IsReserved(asciiHost, out string? tld))
        {
            return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                UrlSafetyViolationKind.ReservedTld,
                $"Host '{asciiHost}' uses the reserved TLD '{tld}'.",
                "UrlSafety:ReservedTld"));
        }

        // (7) Deny patterns.
        if (optionOverrides.DeniedHostPatterns.Count > 0 && HostPatternMatcher.MatchesAny(asciiHost, optionOverrides.DeniedHostPatterns))
        {
            return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                UrlSafetyViolationKind.HostPatternDenied,
                $"Host '{asciiHost}' matches a denied pattern.",
                "UrlSafety:HostPatternDenied"));
        }

        // (8) Allow patterns (if non-empty, must match at least one).
        if (optionOverrides.AllowedHostPatterns.Count > 0 && !HostPatternMatcher.MatchesAny(asciiHost, optionOverrides.AllowedHostPatterns))
        {
            return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                UrlSafetyViolationKind.HostPatternNotAllowed,
                $"Host '{asciiHost}' is not in the allowed pattern list.",
                "UrlSafety:HostPatternNotAllowed"));
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
                return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                    UrlSafetyViolationKind.DnsResolutionFailed,
                    $"DNS resolution timed out for host '{asciiHost}'.",
                    "UrlSafety:DnsResolutionFailed"));
            }
            catch (SocketException)
            {
                return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                    UrlSafetyViolationKind.DnsResolutionFailed,
                    $"DNS resolution failed for host '{asciiHost}'.",
                    "UrlSafety:DnsResolutionFailed"));
            }

            if (addresses.Length == 0)
            {
                return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                    UrlSafetyViolationKind.DnsResolutionFailed,
                    $"DNS resolution returned no addresses for host '{asciiHost}'.",
                    "UrlSafety:DnsResolutionFailed"));
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

                return UrlSafetyResult.Invalid(new UrlSafetyViolation(
                    kind,
                    BuildReason(kind, asciiHost),
                    LocalizationKeyFor(kind)));
            }
        }

        return UrlSafetyResult.Valid(addresses);
    }

    private static bool IsSchemeAllowed(string scheme, IReadOnlyList<string> allowed)
    {
        for (int i = 0; i < allowed.Count; i++)
        {
            if (string.Equals(scheme, allowed[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string NormalizeHost(string host)
    {
        // IPv6 literal in URI form is wrapped in brackets — Uri.Host already strips them, but defend.
        if (host.Length > 0 && host[0] == '[')
        {
            return host;
        }

        if (IPAddress.TryParse(host, out _))
        {
            return host;
        }

        try
        {
            return IdnMapping.GetAscii(host);
        }
        catch (ArgumentException)
        {
            return host;
        }
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
