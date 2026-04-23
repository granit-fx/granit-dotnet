namespace Granit.Http.Cookies.Endpoints.Dtos;

/// <summary>
/// Response payload for the cookie consent configuration endpoint.
/// Contains all information the front-end needs to configure a CMP.
/// </summary>
/// <param name="Cookies">Internal cookies registered by the application.</param>
/// <param name="Services">Third-party services that set cookies on the client.</param>
public sealed record CookieConsentConfigResponse(
    IReadOnlyList<CookieDefinitionResponse> Cookies,
    IReadOnlyList<ThirdPartyServiceResponse> Services);

/// <summary>
/// API response for an internal cookie definition.
/// </summary>
/// <param name="Name">Cookie name.</param>
/// <param name="Category">GDPR consent category (snake_case).</param>
/// <param name="RetentionDays">Maximum retention period in days.</param>
/// <param name="Purpose">Human-readable purpose description.</param>
public sealed record CookieDefinitionResponse(
    string Name,
    string Category,
    int RetentionDays,
    string Purpose);

/// <summary>
/// API response for a third-party service that sets cookies.
/// </summary>
/// <param name="Name">Service identifier (e.g. "matomo", "hubspot").</param>
/// <param name="Category">GDPR consent category (snake_case).</param>
/// <param name="CookiePatterns">Regex patterns matching cookies set by this service.</param>
public sealed record ThirdPartyServiceResponse(
    string Name,
    string Category,
    IReadOnlyList<string> CookiePatterns);
