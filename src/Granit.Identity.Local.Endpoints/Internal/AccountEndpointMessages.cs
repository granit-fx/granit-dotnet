using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Granit.Identity.Local.Endpoints.Internal;

/// <summary>
/// Resolves account-endpoint problem-detail messages from the
/// <c>IdentityLocalEndpoints</c> localization resource in the request culture.
/// </summary>
/// <remarks>
/// Keeps the wire-facing <c>detail</c> strings translatable (keyed, resolved per request)
/// instead of hardcoded English. Degrades to the supplied English fallback only when the
/// localization layer is not wired (e.g. a minimal test host) — real apps always register
/// the resource via <c>GranitIdentityLocalEndpointsModule</c>.
/// </remarks>
internal static class AccountEndpointMessages
{
    /// <summary>
    /// Returns the localized message for <paramref name="key"/>, or
    /// <paramref name="fallback"/> when the localizer is unavailable or the key is absent.
    /// </summary>
    public static string Localize(HttpContext httpContext, string key, string fallback)
    {
        IStringLocalizer<IdentityLocalEndpointsLocalizationResource>? localizer = httpContext.RequestServices
            .GetService<IStringLocalizer<IdentityLocalEndpointsLocalizationResource>>();

        if (localizer is null)
        {
            return fallback;
        }

        LocalizedString localized = localizer[key];
        return localized.ResourceNotFound ? fallback : localized.Value;
    }
}
