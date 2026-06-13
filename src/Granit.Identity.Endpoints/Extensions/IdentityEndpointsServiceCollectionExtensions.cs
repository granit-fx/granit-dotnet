using Granit.Http.Cookies;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Granit.Identity.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering identity endpoints services.
/// </summary>
public static class IdentityEndpointsServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required by the identity endpoints (webhook validator, health check).
    /// </summary>
    public static IServiceCollection AddGranitIdentityEndpoints(this IServiceCollection services)
    {
        services.AddGranitIdentity();

        services.AddSingleton<WebhookSignatureValidator>();
        services.AddOptions<IdentityWebhookOptions>()
            .BindConfiguration(IdentityWebhookOptions.SectionName);

        // Device trust: the signed-cookie binding service consumed by the manage endpoints, the login step-up
        // decision, and the session-created emission sites.
        services.AddOptions<DeviceTrustOptions>()
            .BindConfiguration(DeviceTrustOptions.SectionName);
        services.AddDataProtection();
        services.AddScoped<IDeviceTrustCookieService, DeviceTrustCookieService>();
        services.AddScoped<DeviceTrustResolutionMiddleware>();
        services.AddSingleton<ICookieDefinitionContributor, DeviceTrustCookieDefinitionContributor>();

        // Session review ("was this you?"): the data-protected token service the alert link and the anonymous
        // review endpoints share. Notifications resolve ISessionReviewTokenService to mint the link.
        services.AddOptions<SessionReviewOptions>()
            .BindConfiguration(SessionReviewOptions.SectionName);
        services.AddScoped<ISessionReviewTokenService, DataProtectionSessionReviewTokenService>();

        services.AddHealthChecks()
            .AddCheck<UserCacheHealthCheck>(
                "identity-user-cache",
                tags: ["readiness", "startup"]);

        return services;
    }
}
