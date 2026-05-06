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

        // Register the API key generator + the versioned hasher it depends on
        services.TryAddSingleton<IApiKeyHasher, ApiKeyHasher>();
        services.TryAddSingleton<IApiKeyGenerator, ApiKeyGenerator>();
        services.TryAddSingleton<ApiKeysMetrics>();

        // Module-level options (lead time for expiring-soon scanner, pepper, etc.).
        // The scanner itself ships in Granit.Authentication.ApiKeys.BackgroundJobs;
        // binding here keeps host configuration in one place.
        services.AddOptions<ApiKeysOptions>()
            .BindConfiguration(ApiKeysOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(ValidatePepper, "ApiKeys:Pepper must be base64-encoded and decode to at least 32 bytes (256 bits) when configured.")
            .ValidateOnStart();

        // Add the authentication scheme
        services.AddAuthentication()
            .AddScheme<ApiKeyOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                configureOptions);

        return services;
    }

    private static bool ValidatePepper(ApiKeysOptions options)
    {
        if (string.IsNullOrEmpty(options.Pepper))
        {
            return true;
        }

        try
        {
            byte[] decoded = Convert.FromBase64String(options.Pepper);
            return decoded.Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
