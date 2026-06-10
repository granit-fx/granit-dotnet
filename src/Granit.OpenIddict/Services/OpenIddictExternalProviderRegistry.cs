using Granit.Identity.Local.Services;
using Granit.OpenIddict.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Services;

/// <summary>
/// OpenIddict implementation of <see cref="IExternalProviderRegistry"/>.
/// Reads configured providers from <see cref="GranitOpenIddictClientOptions"/> and
/// cross-checks them against the authentication handlers the host has actually registered.
/// </summary>
internal sealed class OpenIddictExternalProviderRegistry(
    IOptions<GranitOpenIddictClientOptions> clientOptions,
    IAuthenticationSchemeProvider schemeProvider) : IExternalProviderRegistry
{
    /// <inheritdoc/>
    public IReadOnlyList<string> GetConfiguredProviderNames() =>
        clientOptions.Value.Providers.Select(p => p.Name).ToList();

    /// <inheritdoc/>
    public bool IsProviderConfigured(string providerName) =>
        FindConfigured(providerName) is not null;

    /// <inheritdoc/>
    public async Task<bool> IsProviderAvailableAsync(
        string providerName, CancellationToken cancellationToken = default)
    {
        ExternalProviderOptions? configured = FindConfigured(providerName);
        if (configured is null)
        {
            return false;
        }

        // The authentication scheme the host registers (e.g. AddGoogle()) is named after
        // the provider — resolve it by the canonical configured name so a config/scheme
        // mismatch surfaces here instead of dead-ending at the OAuth redirect.
        AuthenticationScheme? scheme = await schemeProvider
            .GetSchemeAsync(configured.Name).ConfigureAwait(false);
        return scheme is not null;
    }

    private ExternalProviderOptions? FindConfigured(string providerName) =>
        clientOptions.Value.Providers
            .FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
}
