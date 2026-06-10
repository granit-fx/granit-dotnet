using Granit.Authentication.External.Options;
using Granit.Identity.Local.Services;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Services;

/// <summary>
/// <see cref="IExternalProviderRegistry"/> implementation that reads configured external
/// providers from <see cref="ExternalAuthOptions"/> (the <c>Authentication:External</c> section,
/// owned by <c>Granit.Authentication.External</c>).
/// </summary>
internal sealed class OpenIddictExternalProviderRegistry(
    IOptions<ExternalAuthOptions> externalAuthOptions) : IExternalProviderRegistry
{
    /// <inheritdoc/>
    public IReadOnlyList<string> GetConfiguredProviderNames() =>
        externalAuthOptions.Value.Providers.Select(p => p.SchemeName).ToList();

    /// <inheritdoc/>
    public bool IsProviderConfigured(string providerName) =>
        externalAuthOptions.Value.Providers
            .Any(p => p.SchemeName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
}
