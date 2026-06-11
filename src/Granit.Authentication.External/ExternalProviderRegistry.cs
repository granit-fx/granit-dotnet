using Granit.Authentication.External.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.External;

/// <summary>
/// Default <see cref="IExternalProviderRegistry"/> implementation: reads the providers configured
/// under <c>Authentication:External:Providers</c> and cross-checks them against the authentication
/// handlers the host has actually registered. Has no auth-server dependency.
/// </summary>
internal sealed class ExternalProviderRegistry(
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExternalProviderInfo>> GetAvailableProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var available = new List<ExternalProviderInfo>();

        foreach (ExternalAuthProvider provider in externalAuthOptions.Value.Providers)
        {
            AuthenticationScheme? scheme = await schemeProvider
                .GetSchemeAsync(provider.SchemeName).ConfigureAwait(false);

            if (scheme is not null)
            {
                available.Add(new ExternalProviderInfo(
                    provider.SchemeName, provider.Type, provider.ResolvedDisplayName));
            }
        }

        return available;
    }

    private ExternalAuthProvider? FindConfigured(string providerName) =>
        externalAuthOptions.Value.Providers
            .FirstOrDefault(p => p.SchemeName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
}
