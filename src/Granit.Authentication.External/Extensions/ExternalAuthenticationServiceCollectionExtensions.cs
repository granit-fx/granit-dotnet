using Granit.Authentication.External.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authentication.External.Extensions;

/// <summary>
/// Registration helpers for external authentication providers.
/// </summary>
public static class ExternalAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="ExternalAuthOptions"/> from configuration. Called by
    /// <c>GranitAuthenticationExternalModule</c>; provider packages add the actual schemes via
    /// <see cref="AddExternalProviderSchemes"/>.
    /// </summary>
    public static IServiceCollection AddGranitExternalProviders(this IServiceCollection services)
    {
        services.AddOptions<ExternalAuthOptions>()
            .BindConfiguration(ExternalAuthOptions.SectionName);

        return services;
    }

    /// <summary>
    /// Registers one authentication scheme per configured provider of the given
    /// <paramref name="providerType"/>, invoking <paramref name="register"/> to wire each instance.
    /// A no-op when no provider of that type is configured.
    /// </summary>
    /// <remarks>
    /// Provider packages call this from their module so the host opts in simply by referencing the
    /// package and listing the provider under <c>Authentication:External:Providers</c>. The scheme
    /// name is always the provider's <see cref="ExternalAuthProvider.SchemeName"/>, keeping it
    /// aligned with what the framework's external-provider registry and startup guard expect.
    /// </remarks>
    public static IServiceCollection AddExternalProviderSchemes(
        this IServiceCollection services,
        IConfiguration configuration,
        string providerType,
        Action<AuthenticationBuilder, ExternalAuthProvider> register)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(providerType);
        ArgumentNullException.ThrowIfNull(register);

        ExternalAuthOptions options =
            configuration.GetSection(ExternalAuthOptions.SectionName).Get<ExternalAuthOptions>()
            ?? new ExternalAuthOptions();

        List<ExternalAuthProvider> matching =
            [.. options.Providers.Where(p =>
                string.Equals(p.Type, providerType, StringComparison.OrdinalIgnoreCase))];

        if (matching.Count == 0)
        {
            return services;
        }

        // AddAuthentication() is idempotent — returns the builder Granit's Identity/OpenIddict
        // modules already configured, so we only append schemes (defaults untouched).
        AuthenticationBuilder authBuilder = services.AddAuthentication();

        foreach (ExternalAuthProvider provider in matching)
        {
            register(authBuilder, provider);
        }

        return services;
    }
}
