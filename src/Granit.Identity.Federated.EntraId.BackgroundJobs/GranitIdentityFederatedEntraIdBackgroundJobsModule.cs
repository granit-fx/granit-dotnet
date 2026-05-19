using Granit.BackgroundJobs;
using Granit.Modularity;

namespace Granit.Identity.Federated.EntraId.BackgroundJobs;

/// <summary>
/// Registers the recurring Entra ID App Role sync job. The handler reuses the Phase 2
/// <c>EntraIdClientRoleSyncService</c> registered by <c>AddGranitIdentityEntraId</c>
/// — no additional DI wiring required here.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitIdentityFederatedEntraIdModule))]
public sealed class GranitIdentityFederatedEntraIdBackgroundJobsModule : GranitModule;
