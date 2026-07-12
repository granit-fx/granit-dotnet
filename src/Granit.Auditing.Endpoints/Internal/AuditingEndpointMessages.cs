using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Granit.Auditing.Endpoints.Internal;

/// <summary>
/// Resolves auditing-endpoint problem-detail messages from the <c>AuditingEndpoints</c>
/// localization resource in the request culture.
/// </summary>
/// <remarks>
/// Keeps the wire-facing <c>detail</c> strings translatable (keyed, resolved per request)
/// instead of hardcoded English. Degrades to the supplied English fallback only when the
/// localization layer is not wired (e.g. a minimal test host) — real apps always register
/// the resource via <c>GranitAuditingEndpointsModule</c>.
/// </remarks>
internal static class AuditingEndpointMessages
{
    /// <summary>
    /// Returns the localized message for <paramref name="key"/>, or
    /// <paramref name="fallback"/> when the localizer is unavailable or the key is absent.
    /// </summary>
    public static string Localize(HttpContext httpContext, string key, string fallback)
    {
        IStringLocalizer<AuditingEndpointsLocalizationResource>? localizer = httpContext.RequestServices
            .GetService<IStringLocalizer<AuditingEndpointsLocalizationResource>>();

        if (localizer is null)
        {
            return fallback;
        }

        LocalizedString localized = localizer[key];
        return localized.ResourceNotFound ? fallback : localized.Value;
    }
}
