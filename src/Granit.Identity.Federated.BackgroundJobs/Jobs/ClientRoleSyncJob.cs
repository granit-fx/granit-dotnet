using Granit.BackgroundJobs;

namespace Granit.Identity.Federated.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that re-runs the federated client-role sync so drift between each configured
/// identity provider and <c>RoleMetadata</c> is bounded by the job cadence rather than by host
/// uptime. Defaults to every 15 minutes — see ADR-030.
/// </summary>
/// <remarks>
/// Additive to the boot-time <c>ClientRoleSyncContributor</c>; both drive every registered
/// provider's <c>IClientRoleSyncPolicy</c> through the shared <c>ClientRoleSyncEngine</c> and
/// therefore inherit the same idempotent upsert semantics and orphan-policy dispatch (ADR-029).
/// A single job replaces the former per-provider jobs; with no provider wired it is a no-op.
/// </remarks>
[RecurringJob("*/15 * * * *", "federated-client-role-sync")]
public sealed record ClientRoleSyncJob : IBackgroundJob;
