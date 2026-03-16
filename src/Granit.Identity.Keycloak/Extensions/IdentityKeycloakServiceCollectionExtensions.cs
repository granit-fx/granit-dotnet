using Granit.Core.Diagnostics;
using Granit.HttpResilience.Extensions;
using Granit.Identity.Extensions;
using Granit.Identity.Keycloak.HealthChecks;
using Granit.Identity.Keycloak.Internal;
using Granit.Identity.Keycloak.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Identity.Keycloak.Extensions;

/// <summary>
/// Extension methods for registering the Keycloak identity provider.
/// </summary>
public static class IdentityKeycloakServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Keycloak Admin API as the <see cref="IIdentityProvider"/> implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Requires the <c>KeycloakAdmin</c> configuration section with:
    /// <c>BaseUrl</c>, <c>Realm</c>, <c>ClientId</c>, <c>ClientSecret</c>.
    /// </para>
    /// <para>
    /// Minimum required role on the service account: <c>realm-management:view-users</c>.
    /// Additional roles depending on features used:
    /// <list type="bullet">
    ///   <item><description><c>realm-management:manage-users</c> — for <c>SetUserEnabledAsync</c>.</description></item>
    ///   <item><description><c>realm-management:impersonation</c> + feature <c>admin-fine-grained-authz</c> — when <c>UseTokenExchangeForDeviceActivity</c> is <c>true</c>.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitIdentityKeycloak(
        this IServiceCollection services)
    {
        GranitActivitySourceRegistry.Register(Diagnostics.IdentityKeycloakActivitySource.Name);

        services.AddOptions<KeycloakAdminOptions>()
            .BindConfiguration(KeycloakAdminOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddGranitHttpClient("KeycloakAdmin", (sp, client) =>
        {
            KeycloakAdminOptions opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeycloakAdminOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        services.TryAddSingleton<KeycloakAdminTokenService>();
        services.TryAddTransient<KeycloakUserTokenExchangeService>();
        services.AddIdentityProvider<KeycloakIdentityProvider>();
        services.Replace(ServiceDescriptor.Scoped<IIdentityProviderCapabilities, KeycloakIdentityProviderCapabilities>());

        return services;
    }

    /// <summary>
    /// Adds a Keycloak connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// Verifies that the token endpoint is reachable and that <c>client_credentials</c>
    /// authentication succeeds.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"keycloak"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    public static IHealthChecksBuilder AddGranitKeycloakHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "keycloak",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<KeycloakHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<KeycloakHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
