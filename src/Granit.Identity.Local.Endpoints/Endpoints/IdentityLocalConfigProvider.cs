using Granit.Identity.Local;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Modularity;
using Granit.Settings.Services;

namespace Granit.Identity.Local.Endpoints.Endpoints;

/// <summary>
/// Maps the <c>Identity.Local.AllowSelfRegistration</c> setting to the public-facing
/// <see cref="IdentityLocalConfigResponse"/>.
/// </summary>
internal sealed class IdentityLocalConfigProvider(ISettingProvider settingProvider)
    : IAsyncModuleConfigProvider<IdentityLocalConfigResponse>
{
    /// <inheritdoc />
    public async Task<IdentityLocalConfigResponse> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        string? value = await settingProvider
            .GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, cancellationToken)
            .ConfigureAwait(false);

        return new(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
    }
}
