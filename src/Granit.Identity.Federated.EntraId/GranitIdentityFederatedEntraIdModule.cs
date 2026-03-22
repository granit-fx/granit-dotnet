using Granit.Core.Modularity;
using Granit.Http.Resilience;
using Granit.Identity.Federated.EntraId.Extensions;
using Granit.Timing;

namespace Granit.Identity.Federated.EntraId;

/// <summary>
/// Granit module that registers the Microsoft Graph API as the
/// <see cref="IIdentityProvider"/> implementation.
/// </summary>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitIdentityFederatedModule),
    typeof(GranitTimingModule))]
public sealed class GranitIdentityFederatedEntraIdModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIdentityEntraId();
}
