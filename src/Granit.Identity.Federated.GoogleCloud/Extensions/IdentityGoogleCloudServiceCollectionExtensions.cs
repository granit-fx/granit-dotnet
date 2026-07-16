using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Granit.Diagnostics;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.Extensions;
using Granit.Identity.Federated.GoogleCloud.HealthChecks;
using Granit.Identity.Federated.GoogleCloud.Internal;
using Granit.Identity.Federated.GoogleCloud.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.GoogleCloud.Extensions;

/// <summary>
/// Extension methods for registering the Google Cloud Identity Platform identity provider.
/// </summary>
public static class IdentityGoogleCloudServiceCollectionExtensions
{
    /// <summary>
    /// Registers Google Cloud Identity Platform (Firebase Auth) as the
    /// <see cref="IIdentityProvider"/> implementation.
    /// </summary>
    public static IServiceCollection AddGranitIdentityGoogleCloud(
        this IServiceCollection services)
    {
        GranitActivitySourceRegistry.Register(Diagnostics.IdentityGoogleCloudActivitySource.Name);

        services.AddOptions<GoogleCloudIdentityOptions>()
            .BindConfiguration(GoogleCloudIdentityOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(sp =>
        {
            GoogleCloudIdentityOptions opts = sp.GetRequiredService<IOptions<GoogleCloudIdentityOptions>>().Value;

            AppOptions appOptions = new()
            {
                ProjectId = opts.ProjectId,
            };

            if (!string.IsNullOrEmpty(opts.CredentialFilePath))
            {
                appOptions.Credential = CredentialFactory
                    .FromFile(opts.CredentialFilePath, JsonCredentialParameters.ServiceAccountCredentialType);
            }

            var app = FirebaseApp.Create(appOptions);
            return FirebaseAuth.GetAuth(app);
        });

        services.TryAddSingleton<IFirebaseAuthTransport, FirebaseAuthTransport>();
        services.AddIdentityProvider<GoogleCloudIdentityProvider>();

        // Wrap IIdentityProvider with graceful degradation (GoogleCloud exposes neither the
        // client-role nor session/device facets, so no concrete-type forwarders are needed).
        services.DecorateIdentityProviderWithGracefulDegradation<GoogleCloudIdentityProvider>();
        services.Replace(ServiceDescriptor.Scoped<IIdentityProviderCapabilities, GoogleCloudIdentityProviderCapabilities>());

        return services;
    }

    /// <summary>
    /// Adds a Google Cloud Identity Platform connectivity health check tagged
    /// <c>"readiness"</c> and <c>"startup"</c>. Verifies that the Firebase Admin SDK
    /// can authenticate and reach Identity Toolkit by reading the first user page
    /// with <c>PageSize = 1</c>.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"googlecloud-identity"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    public static IHealthChecksBuilder AddGranitGoogleCloudIdentityHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "googlecloud-identity",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<GoogleCloudIdentityHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<GoogleCloudIdentityHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
