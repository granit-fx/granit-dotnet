namespace Granit.Http.Cookies;

/// <summary>
/// Immutable definition of a third-party service that sets cookies.
/// Declared in configuration and exposed to the front-end for CMP setup.
/// </summary>
/// <param name="Name">Unique service identifier (e.g. "matomo", "hubspot").</param>
/// <param name="Category">RGPD consent category this service belongs to.</param>
/// <param name="CookiePatterns">
/// Optional regex patterns matching the cookies set by this service (e.g. "^_pk_").
/// Used by CMPs to clean up cookies when consent is revoked.
/// </param>
public sealed record ThirdPartyServiceDefinition(
    string Name,
    CookieCategory Category,
    IReadOnlyList<string> CookiePatterns);
