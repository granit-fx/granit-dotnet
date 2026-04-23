---
title: "ADR-030: Client-role sync — scheduled re-sync via Granit.BackgroundJobs"
description: "Extend the Phase 2 boot-time client-role sync with per-provider recurring jobs so drift between provider and RoleMetadata is bounded by the job cadence rather than by host uptime"
sidebar:
  order: 30
  label: "030 - Scheduled re-sync"
---

> **Date:** 2026-04-23
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Identity.Federated.Keycloak.BackgroundJobs`,
> `Granit.Identity.Federated.EntraId.BackgroundJobs`,
> `Granit.Identity.Federated.Cognito.BackgroundJobs`

## Context

Phase 2 (ADR-025 / ADR-026 / ADR-027) ships a boot-time client-role sync:
an `IHostDataSeedContributor` that runs once per host restart. In
steady-state production, hosts restart rarely — which means drift between
the upstream provider and `RoleMetadata` can accumulate for hours or days
before the next restart catches it. ADR-029 added orphan handling but
didn't change when the sync runs.

Phase 3 closes the cadence gap with a **recurring job per provider**.

## Decision

### Per-provider sub-project, one job each

Following the Granit convention for background jobs
(`src/Granit.{Module}.BackgroundJobs/`), each federated provider gets a
dedicated sub-package:

- `Granit.Identity.Federated.Keycloak.BackgroundJobs` →
  `KeycloakClientRoleSyncJob` + handler, job name
  `keycloak-client-role-sync`.
- `Granit.Identity.Federated.EntraId.BackgroundJobs` →
  `EntraIdClientRoleSyncJob` + handler, job name
  `entraid-client-role-sync`.
- `Granit.Identity.Federated.Cognito.BackgroundJobs` →
  `CognitoClientRoleSyncJob` + handler, job name
  `cognito-client-role-sync`.

Each sub-project depends on `Granit.BackgroundJobs` and on the provider
package. Hosts that don't need scheduled re-sync simply don't reference
the sub-package — no dead code ships with the base provider.

### Default cadence: every 15 minutes

`[RecurringJob("*/15 * * * *", ...)]` on each job. Rationale:

- Long enough to batch legitimate role changes by an admin without
  hammering the provider's admin API or Graph.
- Short enough to catch drift within one SLA-friendly window (most
  monitoring / on-call dashboards bucket in 5–15 min intervals).
- Matches the cadence the Phase 2 plan proposed without further study.

The cron expression is hard-coded in the attribute per Granit's
`RecurringJobDiscovery` reflection pattern. Hosts needing a different
cadence fork the attribute or override the
`RecurringJobRegistration` at DI registration time — documented in the
per-package README but not a first-class option surface in this ADR. If
demand materializes, a follow-up could introduce per-provider
`Schedule` option support.

### Additive to the boot-time contributor

Both the boot-time `*ClientRoleSyncContributor` and the recurring
`*ClientRoleSyncJob` delegate to the **same**
`*ClientRoleSyncService.SyncAsync`. Consequences:

- No duplicated sync logic.
- Orphan-policy dispatch (ADR-029), idempotent upsert semantics, and
  restore-on-return all inherit automatically.
- Hosts opting out of scheduled re-sync keep the Phase 2 behaviour
  unchanged; the sub-package registration is additive.

### Public promotion of the sync service

The sync service (`*ClientRoleSyncService`) was internal in Phase 2
(lived in `Internal/Sync/`). To satisfy the Wolverine handler
convention (`public` handler with `public static HandleAsync`), the
service parameter must be publicly visible — `public` methods cannot
take internal parameters (CS0051). Three options were considered:

1. **Make the service public and move it out of `Internal/Sync/`**
   (chosen). Namespace becomes `Granit.Identity.Federated.*.Sync`.
   Architecture convention `Public_types_should_not_reside_in_Internal_namespaces`
   stays clean.
2. Make the handler internal — blocked by CLAUDE.md: Wolverine discovers
   handlers via `Assembly.ExportedTypes` which requires `public`.
3. Introduce a wrapper interface (`IClientRoleSyncDispatcher`) in the
   provider package and have the handler take that — extra indirection
   with no consumer need.

Option 1 is the smallest surface expansion and keeps the code path
identical. The service is meant to be DI-resolvable from downstream
BackgroundJobs packages, so `public` is the honest accessibility.

The contributors stay internal (they're DI-registered by type, never
resolved directly by callers).

### Per-provider sub-package is registered explicitly

Hosts opt in by referencing the sub-package and registering the module:

```csharp
services.AddGranitIdentityKeycloak();                      // Phase 2
// in the module registration graph:
// GranitIdentityFederatedKeycloakBackgroundJobsModule
```

`[RecurringJob]` attribute discovery picks up the job at startup via
`RecurringJobDiscovery.Discover(...)` which scans assemblies for types
carrying the attribute. No explicit `services.AddJob<T>()` call is
needed.

### Enabled flag is shared

The existing `*ClientRoleSyncOptions.Enabled` governs **both** the
boot-time and recurring paths. Rationale:

- One switch for "I want client-role sync on this provider" is the
  correct user-facing semantic.
- Separate on/off for boot-time vs recurring is a distinction without
  a meaningful use case — if you want no sync, you turn it off
  everywhere; if you want only boot-time, you don't reference the
  `.BackgroundJobs` sub-package.

## Consequences

### Positive

- Drift between provider and `RoleMetadata` is now bounded by 15 minutes
  instead of host restart cadence.
- Orphan soft-delete / hard-delete (ADR-029) runs on the same schedule —
  admin-initiated role removals upstream flow through the configured
  policy within the same 15-minute window.
- The sub-package opt-in means hosts with CI-tight deploys (hot
  rolling restarts, GitOps drift-aware) can skip scheduling entirely.

### Negative

- 3 new NuGet packages. Each is small (1 job + 1 handler + 1 module
  class); the footprint is the release train, not the code.
- The cron cadence is not runtime-configurable — host ops need to
  accept the 15-minute default or fork the attribute. Acceptable for
  v1; `Schedule` option support is a fast-follow candidate if demand
  materializes.
- Sync services promoted to `public`. Minor API surface expansion;
  they were always the expected DI resolution target for downstream
  jobs, so this is more a formalization than a new commitment.

### Neutral

- In a multi-replica deployment, each replica runs its own copy of
  the recurring job every 15 minutes. The sync is idempotent (unique
  index + find-then-upsert), so concurrent runs are safe but wasteful.
  A future `[Singleton]`-style scheduling hint (or
  `Granit.BackgroundJobs.Wolverine` leader-election) can collapse to
  single-replica execution — tracked as a follow-up, not a blocker.

## Rollout

1. **This PR** (#1120): 3 `.BackgroundJobs` sub-packages + tests + ADR.
   Default stays "not referenced" — zero behaviour change for existing
   consumers.
2. **Follow-up (opt-in runtime schedule)**: expose `Schedule` on each
   `*ClientRoleSyncOptions` if ops teams ask for it.
3. **Follow-up (single-replica scheduling)**: collaborate with
   `Granit.BackgroundJobs.Wolverine` leader-election to avoid
   duplicate executions across replicas.

## References

- ADR-025 / ADR-026 / ADR-027 — Phase 2 boot-time client-role sync.
- ADR-029 — Orphan cleanup policy (layered on top, inherited
  transparently by the recurring path).
- #1114 — RoleMetadata Phase 2b / 3 epic.
- #1120 — this ADR's implementing PR (story).
