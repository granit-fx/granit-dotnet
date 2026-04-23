using Granit.BackgroundJobs;

namespace Granit.Identity.Federated.Keycloak.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that re-runs the Phase 2 Keycloak client-role sync so drift between
/// Keycloak and <c>RoleMetadata</c> is bounded by the job cadence rather than by host
/// uptime. Defaults to every 15 minutes — see ADR-030.
/// </summary>
/// <remarks>
/// Additive to the boot-time <c>KeycloakClientRoleSyncContributor</c>; both delegate
/// to the same <c>KeycloakClientRoleSyncService.SyncAsync</c> and therefore inherit
/// the same idempotent upsert semantics and orphan-policy dispatch (ADR-029).
/// The sync itself is gated by <c>KeycloakClientRoleSyncOptions.Enabled</c>.
/// </remarks>
[RecurringJob("*/15 * * * *", "keycloak-client-role-sync")]
public sealed record KeycloakClientRoleSyncJob : IBackgroundJob;
