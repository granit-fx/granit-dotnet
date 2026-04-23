using Granit.BackgroundJobs;

namespace Granit.Identity.Federated.Cognito.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that re-runs the Phase 2 Cognito app-client group sync so drift
/// between Cognito User Pool groups and <c>RoleMetadata</c> is bounded by the job
/// cadence rather than by host uptime. Defaults to every 15 minutes — see ADR-030.
/// </summary>
/// <remarks>
/// Additive to the boot-time <c>CognitoClientRoleSyncContributor</c>; both delegate
/// to the same <c>CognitoClientRoleSyncService.SyncAsync</c> and therefore inherit
/// the same naming-prefix filter (ADR-027) and orphan-policy dispatch (ADR-029).
/// The sync itself is gated by <c>CognitoClientRoleSyncOptions.Enabled</c>.
/// </remarks>
[RecurringJob("*/15 * * * *", "cognito-client-role-sync")]
public sealed record CognitoClientRoleSyncJob : IBackgroundJob;
