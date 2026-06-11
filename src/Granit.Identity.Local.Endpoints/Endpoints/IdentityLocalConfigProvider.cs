using Granit.Authentication.External;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Modularity;
using Granit.Settings.Services;

namespace Granit.Identity.Local.Endpoints.Endpoints;

/// <summary>
/// Builds the public-facing <see cref="IdentityLocalConfigResponse"/> from the
/// <c>Identity.Local.AllowSelfRegistration</c> setting and the available external providers.
/// </summary>
/// <remarks>
/// <paramref name="externalProviderRegistry"/> is optional: it is registered by the auth-server
/// (e.g. <c>Granit.OpenIddict</c>). When absent, no external providers are reported.
/// </remarks>
internal sealed class IdentityLocalConfigProvider(
    ISettingProvider settingProvider,
    IExternalProviderRegistry? externalProviderRegistry = null)
    : IAsyncModuleConfigProvider<IdentityLocalConfigResponse>
{
    /// <inheritdoc />
    public async Task<IdentityLocalConfigResponse> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        string? value = await settingProvider
            .GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ExternalLoginProviderInfo> providers = [];
        if (externalProviderRegistry is not null)
        {
            IReadOnlyList<ExternalProviderInfo> available = await externalProviderRegistry
                .GetAvailableProvidersAsync(cancellationToken).ConfigureAwait(false);
            providers = [.. available.Select(p => new ExternalLoginProviderInfo(p.Name, p.Type, p.DisplayName))];
        }

        return new(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase), providers);
    }
}
