using Granit.Authorization;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.MultiTenancy.Authorization.Extensions;
using Granit.MultiTenancy.Authorization.Internal;

namespace Granit.MultiTenancy.Authorization;

/// <summary>
/// Granit module wiring the Host impersonation gate to the permission system.
/// Registering this module enables <c>MultiTenancy.Host.Impersonate</c> as a
/// permission and swaps the deny-all default for the permission-based gate.
/// </summary>
[DependsOn(typeof(GranitAuthorizationModule))]
[DependsOn(typeof(GranitMultiTenancyModule))]
public sealed class GranitMultiTenancyAuthorizationModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<MultiTenancyAuthorizationLocalizationResource>();
        context.Services.AddGranitHostImpersonationWithPermissions();
    }
}
