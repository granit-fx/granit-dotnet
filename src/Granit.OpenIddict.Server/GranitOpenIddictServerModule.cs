using Granit.Authentication;
using Granit.Authentication.DPoP;
using Granit.Authentication.DPoP.Options;
using Granit.Modularity;
using Granit.OpenIddict.Server.Handlers;
using Granit.OpenIddict.Server.Internal;
using Granit.UserSessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Server;

/// <summary>
/// Granit module that configures the OpenIddict OIDC server:
/// endpoint URIs, flows, signing keys, custom grant types, and ASP.NET Core integration.
/// </summary>
[DependsOn(
    typeof(GranitAuthenticationModule),
    typeof(GranitAuthenticationDPoPModule),
    typeof(GranitOpenIddictModule),
    typeof(GranitUserSessionsAbstractionsModule))]
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
        context.Services.TryAddScoped<OpenIddictUserSessionCreatedHandler>();

        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<DPoPValidationOptions>, Fapi2DPoPOptionsConfigurator>());
    }
}
