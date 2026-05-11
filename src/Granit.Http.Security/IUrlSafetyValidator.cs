using Granit.Http.Security.Options;

namespace Granit.Http.Security;

/// <summary>
/// Validates that an outbound URL is safe to contact: not pointing at private networks,
/// loopback, link-local or cloud-metadata addresses; using an allowed scheme; and matching
/// the configured host allow/deny lists. Resistant to DNS rebinding by always returning the
/// runtime-resolved addresses callers must pin connections to.
/// </summary>
/// <remarks>
/// Conforms to OWASP ASVS V12.6.1 (SSRF defenses) and OWASP API Security Top 10 — API7:2023.
/// </remarks>
public interface IUrlSafetyValidator
{
    /// <summary>
    /// Validates <paramref name="url"/> against the ambient
    /// <see cref="UrlSafetyOptions"/> configured in DI.
    /// </summary>
    ValueTask<UrlSafetyResult> ValidateAsync(Uri url, CancellationToken ct = default);

    /// <summary>
    /// Validates <paramref name="url"/> against a caller-supplied override of the options.
    /// Useful when a single host (e.g. a webhook subscription, a browsing target) needs
    /// stricter or looser rules than the global defaults.
    /// </summary>
    ValueTask<UrlSafetyResult> ValidateAsync(Uri url, UrlSafetyOptions optionOverrides, CancellationToken ct = default);
}
