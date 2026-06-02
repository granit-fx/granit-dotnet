using Granit.Http.Cookies;
using Granit.Privacy.Endpoints.Endpoints;
using Granit.Privacy.Endpoints.Options;
using Granit.Privacy.OptOut;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Privacy.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping privacy endpoints.
/// </summary>
public static class PrivacyEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps privacy endpoints under <c>/{prefix}/privacy</c>: data export (Art. 15/20),
    /// data deletion (Art. 17), and legal agreement consent (Art. 7).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="PrivacyEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitPrivacy(
        this IEndpointRouteBuilder endpoints,
        Action<PrivacyEndpointsOptions>? configure = null)
    {
        PrivacyEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        if (!string.IsNullOrEmpty(options.RateLimitingPolicy))
        {
            group.RequireRateLimiting(options.RateLimitingPolicy);
        }

        RegisterOptOutCookie(endpoints.ServiceProvider.GetRequiredService<ICookieRegistry>());

        group.MapPrivacyRegulationEndpoints();
        group.MapPrivacyPurposesEndpoints();
        group.MapPrivacyOptOutEndpoints();
        group.MapPrivacyExportEndpoints();
        group.MapPrivacyDeletionEndpoints();
        group.MapPrivacyAgreementEndpoints();
        group.MapGranitGroup("/legal-documents").MapLegalDocumentAdminEndpoints();

        return group;
    }

    internal static void RegisterOptOutCookie(ICookieRegistry registry) =>
        registry.Register(new CookieDefinition(
            OptOutConstants.CookieName,
            CookieCategory.StrictlyNecessary,
            OptOutConstants.RetentionDays,
            true,
            "CCPA anonymous opt-out tracking identifier"));
}
