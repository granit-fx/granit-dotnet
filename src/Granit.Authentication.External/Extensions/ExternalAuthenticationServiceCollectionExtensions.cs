using Granit.Authentication.External.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // The registry only needs the bound options + the registered auth schemes — no auth-server
        // dependency — so it lives here, making external-provider availability self-contained.
        services.TryAddSingleton<IExternalProviderRegistry, ExternalProviderRegistry>();

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

        // Skip providers with no ClientId: every external provider (OAuth and OIDC) requires one,
        // and OAuthOptions.Validate() throws on an empty ClientId the moment the remote handler is
        // initialized — which happens on every request, breaking the whole pipeline. A blank entry
        // is the documented "configure later via user-secrets" placeholder, so treat it as a no-op
        // until it is actually filled in rather than registering an unusable scheme.
        List<ExternalAuthProvider> matching =
            [.. options.Providers.Where(p =>
                string.Equals(p.Type, providerType, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(p.ClientId))];

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
