using Microsoft.AspNetCore.Http;

namespace Granit.Http.Cookies;

/// <summary>
/// Resolves whether consent has been granted for a given cookie category.
/// Applications provide their own implementation (@cookieconsent/core via Granit.Http.Cookies.CookieConsent, Axeptio, Cookiebot, native, etc.).
/// </summary>
public interface IConsentResolver
{
    /// <summary>
    /// Returns <c>true</c> if the user has granted consent for the specified category.
    /// </summary>
    Task<bool> HasConsentAsync(HttpContext httpContext, CookieCategory category);
}
