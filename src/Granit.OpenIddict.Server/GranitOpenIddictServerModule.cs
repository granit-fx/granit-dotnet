using Granit.Authentication;
using Granit.Authentication.OpenIddict;
using Granit.Identity;
using Granit.Modularity;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.Handlers;
using Granit.OpenIddict.Server.Internal;
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
    typeof(GranitAuthenticationOpenIddictModule),
    typeof(GranitIdentityAbstractionsModule),
    typeof(GranitOpenIddictModule))]
public sealed class GranitOpenIddictServerModule : GranitModule
{
    // Scoped handlers must be registered in DI for OpenIddict to resolve them
    // via UseScopedHandler<T>(). The descriptor itself is attached to the
    // OpenIddict server options in AddGranitOpenIddictServer().
    //
    // The core server is sender-constraining-agnostic: the DPoP token-binding handler and
    // its FAPI 2.0 options configurator live in the opt-in Granit.OpenIddict.Server.DPoP
    // package. SenderConstrainingOptionsValidator fails fast if the configured mode has no
    // mechanism package referenced.
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<ClientSideAuthorizationHandler>();
        context.Services.TryAddScoped<OpenIddictUserSessionCreatedHandler>();

        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<GranitOpenIddictOptions>, SenderConstrainingOptionsValidator>());
    }
}
