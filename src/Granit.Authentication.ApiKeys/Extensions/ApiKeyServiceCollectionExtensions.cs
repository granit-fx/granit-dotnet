using Granit.Authentication.ApiKeys.Diagnostics;
using Granit.Authentication.ApiKeys.Internal;
using Granit.Authentication.ApiKeys.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.ApiKeys.Extensions;

/// <summary>
/// Extension methods for registering API key authentication services.
/// </summary>
public static class ApiKeyServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit API key authentication as an additional authentication scheme
    /// alongside the existing JWT Bearer scheme.
    /// </summary>
    public static IServiceCollection AddGranitApiKeyAuthentication(
        this IServiceCollection services,
        Action<ApiKeyOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the API key generator
        services.TryAddSingleton<IApiKeyGenerator, ApiKeyGenerator>();
        services.TryAddSingleton<ApiKeysMetrics>();

        // Add the authentication scheme
        services.AddAuthentication()
            .AddScheme<ApiKeyOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                configureOptions);

        return services;
    }
}
