using Granit.Modularity;
using Granit.OpenIddict.Server.Mtls.Handlers;
using Granit.OpenIddict.Server.Mtls.Internal;
using Granit.OpenIddict.Server.SenderConstraining;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.OpenIddict.Server.Mtls;

/// <summary>
/// Opt-in mutual-TLS sender-constraining for the Granit OpenIddict server. Referencing this package
/// wires the certificate token-binding handler (cnf.x5t#S256 at <c>/connect/token</c>, RFC 8705 §3)
/// into the server pipeline and declares mTLS as an available sender-constraining mechanism — the
/// core server package stays mTLS-free so DPoP-only or Bearer-only hosts do not pull it in.
/// </summary>
[DependsOn(typeof(GranitOpenIddictServerModule))]
public sealed class GranitOpenIddictServerMtlsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<MtlsTokenBindingHandler>();

        // Declare mTLS as an available sender-constraining mechanism (validated at startup).
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISenderConstrainingMechanism, MtlsSenderConstrainingMechanism>());

        // Attach the token-binding event handler to the OpenIddict server pipeline additively —
        // the core AddGranitOpenIddictServer() does not wire it.
        context.Services.AddOpenIddict()
            .AddServer(options => options.AddEventHandler(MtlsTokenBindingHandler.Descriptor));
    }
}
