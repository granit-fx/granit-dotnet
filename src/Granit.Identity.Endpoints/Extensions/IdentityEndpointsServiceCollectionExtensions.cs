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

        services.AddHealthChecks()
            .AddCheck<UserCacheHealthCheck>(
                "identity-user-cache",
                tags: ["readiness", "startup"]);

        return services;
    }
}
