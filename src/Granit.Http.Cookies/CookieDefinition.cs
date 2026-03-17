namespace Granit.Http.Cookies;

/// <summary>
/// Immutable definition of a registered cookie.
/// Every cookie must be declared at startup via the registry.
/// </summary>
/// <param name="Name">Unique cookie name.</param>
/// <param name="Category">RGPD consent category.</param>
/// <param name="RetentionDays">Maximum retention period in days.</param>
/// <param name="IsHttpOnly">Whether the cookie is inaccessible to JavaScript.</param>
/// <param name="Purpose">Human-readable purpose for RGPD audit trail.</param>
public sealed record CookieDefinition(
    string Name,
    CookieCategory Category,
    int RetentionDays,
    bool IsHttpOnly,
    string Purpose);
