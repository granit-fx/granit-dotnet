using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using Granit.Diagnostics;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.Cognito.HealthChecks;
using Granit.Identity.Federated.Cognito.Internal;
using Granit.Identity.Federated.Cognito.Options;
using Granit.Identity.Federated.Cognito.Sync;
using Granit.Identity.Federated.Extensions;
using Granit.Identity.Federated.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Cognito.Extensions;

/// <summary>
/// Extension methods for registering the AWS Cognito identity provider.
/// </summary>
public static class IdentityCognitoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AWS Cognito User Pools as the <see cref="IIdentityProvider"/> implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitIdentityCognito(
        this IServiceCollection services)
    {
        GranitActivitySourceRegistry.Register(Diagnostics.IdentityCognitoActivitySource.Name);

        services.AddOptions<CognitoAdminOptions>()
            .BindConfiguration(CognitoAdminOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IAmazonCognitoIdentityProvider>(sp =>
        {
            CognitoAdminOptions opts = sp.GetRequiredService<IOptions<CognitoAdminOptions>>().Value;
            AmazonCognitoIdentityProviderConfig config = new()
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(opts.Region),
                Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds),
            };

            if (!string.IsNullOrEmpty(opts.AccessKeyId) && !string.IsNullOrEmpty(opts.SecretAccessKey))
            {
                BasicAWSCredentials credentials = new(opts.AccessKeyId, opts.SecretAccessKey);
                return new AmazonCognitoIdentityProviderClient(credentials, config);
            }

            // Use IAM roles / environment credentials
            return new AmazonCognitoIdentityProviderClient(config);
        });

        services.AddIdentityProvider<CognitoIdentityProvider>();

        // Wrap IIdentityProvider with graceful degradation; registers CognitoIdentityProvider by
        // its concrete type so the facets the decorator does not carry (client-role, session,
        // device) resolve the raw provider directly below.
        services.DecorateIdentityProviderWithGracefulDegradation<CognitoIdentityProvider>();
        services.Replace(ServiceDescriptor.Scoped<IIdentityProviderCapabilities, CognitoIdentityProviderCapabilities>());

        // Client-role capability (Phase 2). Resolves the concrete CognitoIdentityProvider so this
        // facet — which IIdentityProvider does not compose, and the decorator therefore does not
        // implement — shares the SAME scoped instance. See ADR-027.
        services.TryAddScoped<IIdentityClientRoleManager>(sp => sp.GetRequiredService<CognitoIdentityProvider>());

        // Session/device facets. Replace the Null defaults so the canonical /sessions and /devices
        // endpoints surface Cognito data. Both resolve the concrete CognitoIdentityProvider (the
        // decorator does not implement these facets), keeping every facet on one shared instance.
        // Registered at Federated precedence: a co-resident BFF wins the session facet deterministically.
        services.SetUserSessionProvider(
            UserSessionProviderPrecedence.Federated,
            sessionFactory: sp => sp.GetRequiredService<CognitoIdentityProvider>(),
            deviceFactory: sp => sp.GetRequiredService<CognitoIdentityProvider>());

        // Client-role sync pipeline — enumerates prefix-matching Cognito groups for each
        // tracked app-client id at host boot and upserts RoleMetadata rows.
        services.AddOptions<CognitoClientRoleSyncOptions>()
            .BindConfiguration(CognitoClientRoleSyncOptions.SectionName);

        services.AddTransient<IClientRoleSyncPolicy, CognitoClientRoleSyncPolicy>();

        return services;
    }

    /// <summary>
    /// Adds an AWS Cognito connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// Verifies the configured user pool is reachable and the service account can read it.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"cognito"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    public static IHealthChecksBuilder AddGranitCognitoHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "cognito",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<CognitoHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<CognitoHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
