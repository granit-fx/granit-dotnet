using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using Granit.Core.Diagnostics;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.Cognito.Internal;
using Granit.Identity.Federated.Cognito.Options;
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

        return services;
    }
}
