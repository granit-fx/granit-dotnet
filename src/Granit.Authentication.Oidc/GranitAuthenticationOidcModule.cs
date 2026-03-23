using Granit.Authentication.Oidc.Discovery;
using Granit.Authentication.Oidc.Discovery.Internal;
using Granit.Authentication.Oidc.DPoP;
using Granit.Authentication.Oidc.DPoP.Internal;
using Granit.Core.Modularity;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.Oidc;

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
