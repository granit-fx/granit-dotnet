using Granit.Core.Modularity;
using Granit.Http.Resilience;
using Granit.Identity.EntraId.Extensions;
using Granit.Timing;

namespace Granit.Identity.EntraId;

/// <summary>
/// Granit module that registers the Microsoft Graph API as the
/// <see cref="IIdentityProvider"/> implementation.
/// </summary>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitIdentityModule),
    typeof(GranitTimingModule))]
public sealed class GranitIdentityEntraIdModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIdentityEntraId();
}
