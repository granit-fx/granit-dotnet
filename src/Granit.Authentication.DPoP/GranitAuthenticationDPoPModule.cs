using Granit.Authentication.DPoP.Diagnostics;
using Granit.Authentication.DPoP.Extensions;
using Granit.Diagnostics;
using Granit.Modularity;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.DPoP;

/// <summary>
/// Granit module for DPoP proof-of-possession validation (RFC 9449).
/// IdP-agnostic — works with Keycloak, Entra ID, Auth0, OpenIddict, or any OIDC provider.
/// </summary>
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitAuthenticationDPoPModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDPoPValidation();
        context.Services.TryAddSingleton<DPoPValidationMetrics>();
        GranitActivitySourceRegistry.Register(DPoPValidationActivitySource.Name);
    }
}
