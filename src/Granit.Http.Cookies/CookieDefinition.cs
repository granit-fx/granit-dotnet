using Microsoft.AspNetCore.Http;

namespace Granit.Http.Cookies;

/// <summary>
/// Immutable definition of a registered cookie.
/// Every cookie must be declared at startup via the registry.
/// </summary>
/// <param name="Name">Unique cookie name.</param>
/// <param name="Category">GDPR consent category.</param>
/// <param name="RetentionDays">Maximum retention period in days.</param>
/// <param name="IsHttpOnly">Whether the cookie is inaccessible to JavaScript.</param>
/// <param name="Purpose">Human-readable purpose for GDPR audit trail.</param>
public sealed record CookieDefinition(
    string Name,
    CookieCategory Category,
    int RetentionDays,
    bool IsHttpOnly,
    string Purpose)
{
    /// <summary>
    /// Gets the <c>SameSite</c> attribute for the cookie. Default: <see cref="SameSiteMode.Lax"/>.
    /// Security-sensitive cookies (e.g., BFF session) should use <see cref="SameSiteMode.Strict"/>.
    /// </summary>
    public SameSiteMode SameSite { get; init; } = SameSiteMode.Lax;

    /// <summary>
    /// Gets the <c>Path</c> attribute for the cookie. Default: <c>"/"</c>.
    /// Required to be <c>"/"</c> for <c>__Host-</c> prefixed cookies (RFC 6265bis §4.1.3.2).
    /// </summary>
    public string Path { get; init; } = "/";

    /// <summary>
    /// Gets whether the cookie is essential and therefore bypasses GDPR consent suppression.
    /// Derived from <see cref="Category"/>: a cookie is essential <b>if and only if</b> it is
    /// <see cref="CookieCategory.StrictlyNecessary"/>. Coupling the two by construction prevents a
    /// strictly-necessary cookie from being consent-gated, or a consent-gated cookie from being
    /// flagged essential.
    /// </summary>
    public bool IsEssential => Category == CookieCategory.StrictlyNecessary;

    /// <summary>
    /// Optional <c>Domain</c> attribute for the cookie. When set, the cookie is
    /// sent to the specified domain and its subdomains. Used for SSO scenarios
    /// where a single cookie spans multiple apps (e.g., <c>".example.com"</c>).
    /// When <see langword="null"/> (default), the cookie is scoped to the
    /// origin host only — the safer default.
    /// </summary>
    /// <remarks>
    /// Must be propagated to both set and delete operations: omitting Domain on
    /// delete leaves the cookie stranded in the browser when the original set
    /// used an explicit Domain.
    /// </remarks>
    public string? Domain { get; init; }
}
