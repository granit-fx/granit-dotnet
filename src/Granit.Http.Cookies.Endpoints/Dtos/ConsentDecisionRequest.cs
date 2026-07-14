namespace Granit.Http.Cookies.Endpoints.Dtos;

/// <summary>
/// A cookie-consent decision posted by the front-end CMP after the user interacts
/// with the cookie banner.
/// </summary>
/// <remarks>
/// Category names use the snake_case vocabulary of <c>GET /cookies/config</c>
/// (<see cref="CookieCategoryNames"/>). All parameters carry defaults so the OpenAPI
/// schema marks them optional; the validator enforces that at least one category is
/// granted or denied.
/// </remarks>
/// <param name="GrantedCategories">Snake_case names of the categories the user granted.</param>
/// <param name="DeniedCategories">Snake_case names of the categories the user denied.</param>
/// <param name="Mode">Consent model the CMP applied; defaults to <see cref="CookieConsentMode.OptIn"/> (GDPR-safe) when omitted.</param>
/// <param name="CmpSource">Identifier of the CMP that captured the decision.</param>
public sealed record ConsentDecisionRequest(
    IReadOnlyList<string>? GrantedCategories = null,
    IReadOnlyList<string>? DeniedCategories = null,
    CookieConsentMode? Mode = null,
    string CmpSource = "cookieconsent");
