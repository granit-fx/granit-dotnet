using Granit.Authentication;
using Granit.Authentication.DPoP;
using Granit.Modularity;
using Granit.OpenIddict.Server.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.OpenIddict.Server;

/// <summary>
/// Granit module that configures the OpenIddict OIDC server:
/// endpoint URIs, flows, signing keys, custom grant types, and ASP.NET Core integration.
/// </summary>
[DependsOn(
    typeof(GranitAuthenticationModule),
    typeof(GranitAuthenticationDPoPModule),
    typeof(GranitOpenIddictModule))]
public sealed class GranitOpenIddictServerModule : GranitModule
{
    // Scoped handlers must be registered in DI for OpenIddict to resolve them
    // via UseScopedHandler<T>(). The descriptor itself is attached to the
    // OpenIddict server options in AddGranitOpenIddictServer().
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<ClientSideAuthorizationHandler>();
        context.Services.TryAddScoped<DPoPTokenBindingHandler>();
    }
}
