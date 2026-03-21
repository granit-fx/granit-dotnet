using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Granit.Core.Diagnostics;
using Granit.Identity.Extensions;
using Granit.Identity.GoogleCloud.Internal;
using Granit.Identity.GoogleCloud.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Identity.GoogleCloud.Extensions;

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
        services.Replace(ServiceDescriptor.Scoped<IIdentityProviderCapabilities, GoogleCloudIdentityProviderCapabilities>());

        return services;
    }
}
