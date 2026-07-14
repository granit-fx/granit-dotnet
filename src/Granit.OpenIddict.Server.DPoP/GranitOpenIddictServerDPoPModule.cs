using Granit.Authentication.DPoP;
using Granit.Authentication.DPoP.Extensions;
using Granit.Authentication.DPoP.Options;
using Granit.Modularity;
using Granit.OpenIddict.Server.DPoP.Handlers;
using Granit.OpenIddict.Server.DPoP.Internal;
using Granit.OpenIddict.Server.SenderConstraining;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Server.DPoP;

/// <summary>
/// Opt-in DPoP sender-constraining for the Granit OpenIddict server. Referencing this package
/// wires the DPoP token-binding handler (cnf.jkt at <c>/connect/token</c>, RFC 9449 §6) into the
/// server pipeline and pins the FAPI 2.0 DPoP validation flags — the core server package stays
/// DPoP-free so mTLS-only or Bearer-only hosts do not pull in DPoP.
/// </summary>
[DependsOn(
    typeof(GranitAuthenticationDPoPModule),
    typeof(GranitOpenIddictServerModule))]
public sealed class GranitOpenIddictServerDPoPModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The DPoP token-binding handler needs IDPoPProofValidator (not registered by the
        // resource-side module on its own).
        context.Services.AddGranitDPoPProofValidator();
        context.Services.TryAddScoped<DPoPTokenBindingHandler>();

        // Pin the FAPI 2.0 DPoP validation flags when EnableFapi2Profile is set.
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<DPoPValidationOptions>, Fapi2DPoPOptionsConfigurator>());

        // Declare DPoP as an available sender-constraining mechanism (validated at startup).
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISenderConstrainingMechanism, DPoPSenderConstrainingMechanism>());

        // Attach the token-binding event handler to the OpenIddict server pipeline additively —
        // the core AddGranitOpenIddictServer() no longer wires it.
        context.Services.AddOpenIddict()
            .AddServer(options => options.AddEventHandler(DPoPTokenBindingHandler.Descriptor));
    }
}
