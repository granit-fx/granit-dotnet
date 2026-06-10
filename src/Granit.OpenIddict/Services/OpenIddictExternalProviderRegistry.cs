using Granit.Authentication.External.Options;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Services;

/// <summary>
/// <see cref="IExternalProviderRegistry"/> implementation that reads configured external
/// providers from <see cref="ExternalAuthOptions"/> (the <c>Authentication:External</c> section,
/// owned by <c>Granit.Authentication.External</c>) and cross-checks them against the
/// authentication handlers the host has actually registered.
/// </summary>
internal sealed class OpenIddictExternalProviderRegistry(
    IOptions<ExternalAuthOptions> externalAuthOptions,
    IAuthenticationSchemeProvider schemeProvider) : IExternalProviderRegistry
{
    /// <inheritdoc/>
    public IReadOnlyList<string> GetConfiguredProviderNames() =>
        externalAuthOptions.Value.Providers.Select(p => p.SchemeName).ToList();

    /// <inheritdoc/>
    public bool IsProviderConfigured(string providerName) =>
        FindConfigured(providerName) is not null;

    /// <inheritdoc/>
    public async Task<bool> IsProviderAvailableAsync(
        string providerName, CancellationToken cancellationToken = default)
    {
        ExternalAuthProvider? configured = FindConfigured(providerName);
        if (configured is null)
        {
            return false;
        }

        // The scheme the host registers (AddGoogle()/…) is named after the provider's
        // SchemeName — resolve it so a config/scheme mismatch surfaces here instead of
        // dead-ending at the OAuth redirect.
        AuthenticationScheme? scheme = await schemeProvider
            .GetSchemeAsync(configured.SchemeName).ConfigureAwait(false);
        return scheme is not null;
    }

    private ExternalAuthProvider? FindConfigured(string providerName) =>
        externalAuthOptions.Value.Providers
            .FirstOrDefault(p => p.SchemeName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
}
