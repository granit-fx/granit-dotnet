using Granit.Http.Cookies;
using Microsoft.AspNetCore.Http;

namespace Granit.Privacy.Regulations.Cookies.Internal;

/// <summary>
/// Provides cookie consent model information by resolving the current tenant's regulation profile.
/// Memoizes the result per request in <see cref="HttpContext.Items"/> to avoid repeated resolver calls
/// when multiple cookies are written in a single request.
/// </summary>
internal sealed class RegulationBasedCookieConsentModelProvider(
    IPrivacyRegulationResolver regulationResolver) : ICookieConsentModelProvider
{
    private const string CacheKey = "Granit:ConsentModel";

    public async Task<ConsentModelInfo?> GetConsentModelAsync(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(CacheKey, out object? cached) && cached is ConsentModelInfo info)
        {
            return info;
        }

        PrivacyRegulationProfile profile = await regulationResolver
            .ResolveAsync(httpContext.RequestAborted)
            .ConfigureAwait(false);

        ConsentModelInfo result = new(
            MapConsentMode(profile.CookieConsentModel),
            profile.HonorGlobalPrivacyControl);

        httpContext.Items[CacheKey] = result;
        return result;
    }

    private static CookieConsentMode MapConsentMode(ConsentModel model) => model switch
    {
        ConsentModel.OptIn => CookieConsentMode.OptIn,
        ConsentModel.OptOut => CookieConsentMode.OptOut,
        ConsentModel.Hybrid => CookieConsentMode.Hybrid,
        ConsentModel.None => CookieConsentMode.None,
        _ => CookieConsentMode.OptIn,
    };
}
