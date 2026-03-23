using Granit.Authentication.DPoP.Diagnostics;
using Granit.Authentication.DPoP.Extensions;
using Granit.Core.Diagnostics;
using Granit.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authentication.DPoP;

/// <summary>
/// Granit module for DPoP proof-of-possession validation (RFC 9449).
/// IdP-agnostic — works with Keycloak, Entra ID, Auth0, OpenIddict, or any OIDC provider.
/// </summary>
public sealed class GranitAuthenticationDPoPModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDPoPValidation();
        GranitActivitySourceRegistry.Register(DPoPValidationActivitySource.Name);
    }
}
