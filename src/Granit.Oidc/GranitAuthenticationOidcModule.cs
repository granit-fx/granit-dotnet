using Granit.Modularity;
using Granit.Oidc.Discovery;
using Granit.Oidc.Discovery.Internal;
using Granit.Oidc.DPoP;
using Granit.Oidc.DPoP.Internal;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Oidc;

/// <summary>
/// Granit module for OIDC/OAuth 2.0 protocol primitives.
/// Registers <see cref="IDPoPProofService"/> and <see cref="IDiscoveryDocumentService"/> as singletons.
/// </summary>
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitAuthenticationOidcModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<IDPoPProofService, DPoPProofService>();
        context.Services.TryAddSingleton<IDiscoveryDocumentService, DiscoveryDocumentService>();
    }
}
