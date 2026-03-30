using Granit.Http.Cookies.Exceptions;
using Microsoft.AspNetCore.Http;

#pragma warning disable GRSEC004 // This IS the IGranitCookieManager implementation

namespace Granit.Http.Cookies.Internal;

/// <summary>
/// Scoped cookie manager enforcing the Strict Registry Pattern and consent rules.
/// Supports GPC (Global Privacy Control) signal and regulation-aware consent models.
/// </summary>
internal sealed class GranitCookieManager(
    ICookieRegistry registry,
    IConsentResolver consentResolver,
    IGlobalPrivacyControlSignal gpcSignal,
    ICookieConsentModelProvider consentModelProvider) : IGranitCookieManager
{
    /// <inheritdoc/>
    public async Task SetCookieAsync(HttpContext httpContext, string cookieName, string value)
    {
        CookieDefinition definition = registry.GetDefinition(cookieName)
            ?? throw new UnregisteredCookieException(cookieName);

        if (definition.Category != CookieCategory.StrictlyNecessary)
        {
            // GPC check — suppress cookies when GPC is active and jurisdiction requires it
            if (gpcSignal.IsActive(httpContext))
            {
                ConsentModelInfo? model = await consentModelProvider
                    .GetConsentModelAsync(httpContext)
                    .ConfigureAwait(false);

                if (model is not null && ShouldGpcSuppressCategory(model, definition.Category))
                {
                    return;
                }
            }

            bool hasConsent = await consentResolver.HasConsentAsync(httpContext, definition.Category).ConfigureAwait(false);
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
            HttpOnly = definition.IsHttpOnly, // NOSONAR S3330 - intentional: must match SetCookieAsync options for browser to delete the cookie
            Secure = true,
            SameSite = definition.SameSite,
            Path = definition.Path,
        });
    }

    /// <summary>
    /// Determines whether GPC should suppress a cookie category based on the consent model.
    /// CCPA (OptOut): GPC suppresses SaleOrSharing + Marketing only (first-party Analytics preserved).
    /// GDPR (OptIn): GPC suppresses ALL non-essential categories.
    /// </summary>
    private static bool ShouldGpcSuppressCategory(ConsentModelInfo model, CookieCategory category)
    {
        if (!model.HonorGlobalPrivacyControl)
        {
            return false;
        }

        return model.Mode switch
        {
            // CCPA: GPC = "Do Not Sell or Share" — only SaleOrSharing + Marketing
            CookieConsentMode.OptOut => category is CookieCategory.SaleOrSharing or CookieCategory.Marketing,

            // GDPR/LGPD: GPC interpreted as withdrawal of consent for all non-essential
            CookieConsentMode.OptIn => true,

            // Hybrid: same as OptOut — suppress sale/sharing + marketing
            CookieConsentMode.Hybrid => category is CookieCategory.SaleOrSharing or CookieCategory.Marketing,

            // None: no specific consent requirement — respect GPC broadly
            CookieConsentMode.None => true,

            _ => false,
        };
    }
}
