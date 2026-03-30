using Granit.Identity.Local.Services;
using Granit.OpenIddict.Options;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Services;

/// <summary>
/// OpenIddict implementation of <see cref="IExternalProviderRegistry"/>.
/// Reads configured providers from <see cref="GranitOpenIddictClientOptions"/>.
/// </summary>
internal sealed class OpenIddictExternalProviderRegistry(
    IOptions<GranitOpenIddictClientOptions> clientOptions) : IExternalProviderRegistry
{
    /// <inheritdoc/>
    public IReadOnlyList<string> GetConfiguredProviderNames() =>
        clientOptions.Value.Providers.Select(p => p.Name).ToList();

    /// <inheritdoc/>
    public bool IsProviderConfigured(string providerName) =>
        clientOptions.Value.Providers
            .Any(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
}
