using Granit.BackgroundJobs;

namespace Granit.Identity.Federated.EntraId.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that re-runs the Phase 2 Entra ID App Role sync so drift between
/// the Microsoft Graph view of App Roles and <c>RoleMetadata</c> is bounded by the
/// job cadence rather than by host uptime. Defaults to every 15 minutes — see ADR-030.
/// </summary>
/// <remarks>
/// Additive to the boot-time <c>EntraIdClientRoleSyncContributor</c>; both delegate
/// to the same <c>EntraIdClientRoleSyncService.SyncAsync</c> and therefore inherit
/// the same idempotent upsert semantics and orphan-policy dispatch (ADR-029).
/// The sync itself is gated by <c>EntraIdClientRoleSyncOptions.Enabled</c>.
/// </remarks>
[RecurringJob("*/15 * * * *", "entraid-client-role-sync")]
public sealed record EntraIdClientRoleSyncJob : IBackgroundJob;
