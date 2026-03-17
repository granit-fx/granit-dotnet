using Microsoft.AspNetCore.Http;

namespace Granit.Http.Cookies;

/// <summary>
/// Managed cookie operations that enforce the Strict Registry Pattern and RGPD consent.
/// All cookie writes must go through this interface (enforced by analyzer GRSEC004).
/// </summary>
public interface IGranitCookieManager
{
    /// <summary>
    /// Sets a cookie value. The cookie must be registered and consent must be granted
    /// (except for <see cref="CookieCategory.StrictlyNecessary"/> which bypasses consent).
    /// </summary>
    /// <exception cref="Exceptions.UnregisteredCookieException">Thrown when the cookie is not registered.</exception>
    Task SetCookieAsync(HttpContext httpContext, string cookieName, string value);

    /// <summary>
    /// Revokes all cookies in the specified category (deletes them from the response).
    /// </summary>
    Task RevokeCategoryAsync(HttpContext httpContext, CookieCategory category);

    /// <summary>
    /// Deletes a registered cookie.
    /// </summary>
    /// <exception cref="Exceptions.UnregisteredCookieException">Thrown when the cookie is not registered.</exception>
    void DeleteCookie(HttpContext httpContext, string cookieName);
}
