using Granit.BackgroundJobs;
using Granit.Modularity;

namespace Granit.Identity.Federated.BackgroundJobs;

/// <summary>
/// Registers the single recurring federated client-role sync job. The handler drives every
/// registered <c>IClientRoleSyncPolicy</c> through the <c>ClientRoleSyncEngine</c> registered by
/// <c>GranitIdentityFederatedModule</c> — no additional DI wiring required here.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitIdentityFederatedModule))]
public sealed class GranitIdentityFederatedBackgroundJobsModule : GranitModule;
