using Granit.Http.Cookies.Exceptions;
using Microsoft.AspNetCore.Http;

#pragma warning disable GRSEC004 // This IS the IGranitCookieManager implementation

namespace Granit.Http.Cookies.Internal;

/// <summary>
/// Scoped cookie manager enforcing the Strict Registry Pattern and RGPD consent.
/// </summary>
internal sealed class GranitCookieManager(
    ICookieRegistry registry,
    IConsentResolver consentResolver) : IGranitCookieManager
{
    /// <inheritdoc/>
    public async Task SetCookieAsync(HttpContext httpContext, string cookieName, string value)
    {
        CookieDefinition definition = registry.GetDefinition(cookieName)
            ?? throw new UnregisteredCookieException(cookieName);

        if (definition.Category != CookieCategory.StrictlyNecessary)
        {
            bool hasConsent = await consentResolver.ResolveAsync(httpContext, definition.Category).ConfigureAwait(false);
            if (!hasConsent)
            {
                return;
            }
        }

        CookieOptions options = new()
        {
            MaxAge = TimeSpan.FromDays(definition.RetentionDays),
            HttpOnly = definition.IsHttpOnly, // NOSONAR S3330 - intentional: HttpOnly is configurable per cookie (analytics cookies like _ga require JS access)
            Secure = true,
            SameSite = definition.SameSite,
            Path = definition.Path,
            IsEssential = definition.IsEssential,
        };

        httpContext.Response.Cookies.Append(cookieName, value, options);
    }

    /// <inheritdoc/>
    public Task RevokeCategoryAsync(HttpContext httpContext, CookieCategory category)
    {
        IReadOnlyList<CookieDefinition> cookies = registry.GetByCategory(category);

        foreach (CookieDefinition cookie in cookies)
        {
            httpContext.Response.Cookies.Delete(cookie.Name);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void DeleteCookie(HttpContext httpContext, string cookieName)
    {
        CookieDefinition definition = registry.GetDefinition(cookieName)
            ?? throw new UnregisteredCookieException(cookieName);

        httpContext.Response.Cookies.Delete(cookieName, new CookieOptions
        {
            HttpOnly = definition.IsHttpOnly,
            Secure = true,
            SameSite = definition.SameSite,
            Path = definition.Path,
        });
    }
}
