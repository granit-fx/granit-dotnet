using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using Granit.Diagnostics;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.Cognito.Internal;
using Granit.Identity.Federated.Cognito.Options;
using Granit.Identity.Federated.Cognito.Sync;
using Granit.Identity.Federated.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.Replace(ServiceDescriptor.Scoped<IIdentityProviderCapabilities, CognitoIdentityProviderCapabilities>());

        // Client-role capability (Phase 2). Forwards to the scoped IIdentityProvider so
        // IIdentityProvider and IIdentityClientRoleManager resolve to the SAME scoped
        // CognitoIdentityProvider instance — keeps internal state coherent across the
        // two facets. See ADR-027.
        services.TryAddScoped<IIdentityClientRoleManager>(sp =>
            sp.GetRequiredService<IIdentityProvider>() as IIdentityClientRoleManager
            ?? throw new InvalidOperationException(
                "The registered IIdentityProvider does not implement IIdentityClientRoleManager — " +
                "AddGranitIdentityCognito must be the last provider-registration call."));

        // Session/device facets. Replace the Null defaults so the canonical /sessions and /devices
        // endpoints surface Cognito data. Both forward to the SAME scoped IIdentityProvider instance
        // (mirroring IIdentityClientRoleManager above) so every facet shares one CognitoIdentityProvider.
        // Registered at Federated precedence: a co-resident BFF wins the session facet deterministically.
        services.SetUserSessionProvider(
            UserSessionProviderPrecedence.Federated,
            sessionFactory: sp => (IUserSessionProvider)sp.GetRequiredService<IIdentityProvider>(),
            deviceFactory: sp => (IUserDeviceProvider)sp.GetRequiredService<IIdentityProvider>());

        // Client-role sync pipeline — enumerates prefix-matching Cognito groups for each
        // tracked app-client id at host boot and upserts RoleMetadata rows.
        services.AddOptions<CognitoClientRoleSyncOptions>()
            .BindConfiguration(CognitoClientRoleSyncOptions.SectionName);

        services.AddTransient<IClientRoleSyncPolicy, CognitoClientRoleSyncPolicy>();

        return services;
    }
}
