using Granit.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.Keycloak.HealthChecks;
using Granit.Identity.Federated.Keycloak.Internal;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Identity.Federated.Keycloak.Sync;
using Granit.Identity.Federated.RateLimiting;
using Granit.Persistence.DataSeeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Identity.Federated.Keycloak.Extensions;

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

        // Defensive — KeycloakUserTokenExchangeService depends on TimeProvider for
        // rate-limit / refresh-token clock decisions (see PR #1139). Register the
        // system provider so hosts that don't compose Granit.Timing still resolve
        // cleanly. Production hosts typically get this from AddGranitTiming; the
        // TryAddSingleton keeps the default when already provided.
        services.TryAddSingleton(TimeProvider.System);

        services.TryAddSingleton<KeycloakAdminTokenService>();
        services.TryAddTransient<KeycloakUserTokenExchangeService>();

        // Defensive registration: hosts that wire Keycloak via the DI extension (rather
        // than via the module loader) still get the no-op rate limiter so the
        // KeycloakUserTokenExchangeService dependency resolves. Production hosts should
        // replace this with a Granit.RateLimiting-backed implementation — otherwise the
        // RFC 8693 naked-impersonation flow is unbounded per target user.
        services.TryAddSingleton<ITokenExchangeRateLimiter, NullTokenExchangeRateLimiter>();
        services.AddIdentityProvider<KeycloakIdentityProvider>();
        services.Replace(ServiceDescriptor.Scoped<IIdentityProviderCapabilities, KeycloakIdentityProviderCapabilities>());

        // Client-role capability (Phase 2). Forwards to the scoped IIdentityProvider so
        // IIdentityProvider and IIdentityClientRoleManager resolve to the SAME scoped
        // KeycloakIdentityProvider instance — avoids doubling the admin-token acquisition
        // and keeps internal state coherent across the two facets.
        services.TryAddScoped<IIdentityClientRoleManager>(sp =>
            sp.GetRequiredService<IIdentityProvider>() as IIdentityClientRoleManager
            ?? throw new InvalidOperationException(
                "The registered IIdentityProvider does not implement IIdentityClientRoleManager — " +
                "AddGranitIdentityKeycloak must be the last provider-registration call."));

        // Session/device facets. Replace the Null defaults (from the Identity Abstractions module)
        // so the canonical /sessions and /devices APIs surface Keycloak SSO sessions and devices —
        // and revoke them via the Admin API. Both forward to the same scoped IIdentityProvider so
        // every facet resolves to the SAME KeycloakIdentityProvider instance, sharing its admin-token
        // acquisition (same instance-sharing rationale as IIdentityClientRoleManager above).
        // Registered at Federated precedence: a co-resident BFF wins the session facet deterministically.
        services.SetUserSessionProvider(
            UserSessionProviderPrecedence.Federated,
            sessionFactory: sp => (IUserSessionProvider)sp.GetRequiredService<IIdentityProvider>(),
            deviceFactory: sp => (IUserDeviceProvider)sp.GetRequiredService<IIdentityProvider>());

        // Client-role sync pipeline — enumerates Keycloak client roles for each tracked
        // clientId at host boot and upserts RoleMetadata rows. See ADR-025 for details.
        services.AddOptions<KeycloakClientRoleSyncOptions>()
            .BindConfiguration(KeycloakClientRoleSyncOptions.SectionName);

        services.TryAddScoped<KeycloakClientRoleSyncService>();
        services.AddTransient<IHostDataSeedContributor, KeycloakClientRoleSyncContributor>();

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
