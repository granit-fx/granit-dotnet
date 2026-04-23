---
title: "ADR-029: Client-role sync — opt-in orphan cleanup policy"
description: "Add an OrphanedRolePolicy (KeepAndLog / SoftDelete / HardDelete) to the Phase 2 client-role sync pipeline, with soft-delete as the first safe step-up from the default keep-and-log behaviour"
sidebar:
  order: 29
  label: "029 - Orphan cleanup policy"
---

> **Date:** 2026-04-23
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Authorization`, `Granit.Authorization.EntityFrameworkCore`,
> `Granit.Identity.Federated.Keycloak`, `Granit.Identity.Federated.EntraId`,
> `Granit.Identity.Federated.Cognito`

## Context

ADR-025 (Keycloak), ADR-026 (Entra ID) and ADR-027 (Cognito) intentionally
left the question of "what happens when a client role disappears upstream"
unresolved. Phase 2 picked the safest short-term behaviour: leave the
`RoleMetadata` row alone, log the drift, move on. Reason: `PermissionGrant`
has an `ON DELETE CASCADE` FK onto `RoleMetadata`, so removing the metadata
silently strips permissions from live users — exactly the kind of change
that slips past tests and pages operators at 02:00.

Phase 3 adds an **opt-in** policy so teams that want stricter hygiene
(compliance, audit, DR drills) can pick between three explicit behaviours
rather than patching a custom script on top of the sync.

## Decision

### Three-valued policy enum (shared across providers)

```csharp
public enum OrphanedRolePolicy
{
    // Default — Phase 2 behaviour. Drift logged at Information, row preserved.
    KeepAndLog = 0,

    // Mark the row as orphaned: sets IsOrphaned = true, OrphanedAt = <sync time>.
    // PermissionGrant cascades stay intact; `FindByNameAsync` keeps resolving
    // the row. An admin can review and re-trigger a restore or a hard-delete.
    SoftDelete = 1,

    // Physically remove the RoleMetadata row. Cascades onto PermissionGrant.
    // Only use when you have external reconciliation (IaC that rewrites grants,
    // external SSO mapping, or a migration where the grants are known bad).
    HardDelete = 2,
}
```

The enum lives in `Granit.Authorization` so all three provider packages can
reference it. Each provider's `*ClientRoleSyncOptions` gains a single
property:

```csharp
public OrphanedRolePolicy OrphanedRolePolicy { get; set; } = OrphanedRolePolicy.KeepAndLog;
```

Default stays `KeepAndLog` — zero-config upgrade; no existing consumer
changes behaviour unless they opt in.

### Soft-delete state on `RoleMetadata`

Two nullable-friendly columns added to the aggregate:

- `bool IsOrphaned` — defaults to `false`.
- `DateTimeOffset? OrphanedAt` — set when `MarkAsOrphaned` is called.

Two new behaviour methods on the aggregate:

- `MarkAsOrphaned(DateTimeOffset now)` — idempotent: no-op if already
  orphaned, otherwise flips the flag, stamps `OrphanedAt`, raises a
  `RoleOrphanedEvent`.
- `RestoreFromOrphaned()` — called when the sync re-discovers a role
  previously marked orphaned (flip-flop is possible if a Keycloak admin
  removed then re-added the role); clears the flag, raises a
  `RoleRestoredEvent`.

`FindByNameAsync` stays **strict** — it does NOT exclude orphans. Rationale:

- Grants referencing an orphan should keep resolving until an admin
  intervenes. Silent hiding would behave indistinguishably from a hard
  delete from the caller's point of view, which is exactly the footgun we
  want the policy to be explicit about.
- A future admin endpoint (`GET /admin/roles/orphans`, tracked separately)
  is the surface where admins curate the list.

### Orphan detection algorithm

Each sync service adds a "missing from provider" subtraction step:

```text
known    = store.ListByClientIdAsync(trackedClientId)
present  = provider.GetClientRolesAsync(trackedClientId) (by name)

orphans  = known - present        // rows in store not seen upstream
arrivals = present - known        // new roles (existing behaviour)
drift    = known ∩ present        // rename / description changes (existing)
```

The `orphans` set is then processed per the configured policy:

| Policy | Action | Audit |
| ------ | ------ | ----- |
| `KeepAndLog` | No-op write. Log line at Information with name + clientId + last sync time. | Log only. |
| `SoftDelete` | If not already orphaned, call `MarkAsOrphaned(now)` and `UpdateAsync`. | Domain event `RoleOrphanedEvent`; integration event `RoleOrphanedEto` emitted after commit. |
| `HardDelete` | Call `MarkAsDeleted()` then `RemoveAsync`. Cascades onto `PermissionGrant`. | Domain event `RoleDeletedEvent` (existing); integration event `RoleMetadataHardDeletedEto` emitted after commit. |

`arrivals` and `drift` keep their Phase 2 behaviour unchanged.

### New store query: `IRoleMetadataStore.ListByClientIdAsync`

Needed because orphan detection wants a filtered slice of the store per
tracked client, not the full `ListAllAsync`. Signature:

```csharp
Task<IReadOnlyList<RoleMetadata>> ListByClientIdAsync(
    string? clientId, CancellationToken cancellationToken = default);
```

Matches on `ClientId` equality; passing `null` returns the realm-scoped
rows (not used by the sync but keeps the API honest). Uses
`AsNoTracking()` — the sync service loads an aggregate separately via
`FindByNameAsync` when it needs to mutate one, so snapshots are safe.

### Restore-on-return

If the sync sees a name upstream and finds a corresponding `IsOrphaned`
row in the store (same `(Name, TenantId, ClientId)` triplet), it calls
`RestoreFromOrphaned()` and raises `RoleRestoredEvent`. No new row is
created. This closes the flip-flop loop: `admin deletes → next sync marks
orphan → admin re-adds → next sync restores`.

### Failure policy unchanged

Per-provider error handling keeps the Phase 2 "log & skip" contract —
orphan detection runs only after `GetClientRolesAsync` succeeds; a failed
Graph / Keycloak / Cognito call short-circuits the sync for that client
and the store is not mutated.

## Open questions resolved

### Should soft-deleted orphans be auto-hidden from `FindByNameAsync`?

**No.** The whole point of `SoftDelete` vs `HardDelete` is "keep the
permissions working, but flag the row for review". Auto-hiding at lookup
time turns soft-delete into an alternative hard-delete from the caller's
perspective, defeating the separation. Admins curate the orphan list
manually through the future admin endpoint; `FindByNameAsync` stays
source-agnostic.

### Should the sync auto-bump from `KeepAndLog` to `SoftDelete` after N boots?

**No.** That's magic without observability. Operators decide.

## Consequences

### Positive

- Teams that need hygiene get a one-flag upgrade; teams that don't keep
  the Phase 2 default.
- The flip-flop case (admin deletes, re-adds) is handled automatically —
  no operator intervention required.
- Hard-delete path finally has a sanctioned configuration knob rather
  than a "edit the DB directly" recipe.

### Negative

- Two new columns on `authorization_role_metadata`. Existing hosts
  migrating to Phase 3 need an EF migration (additive, nullable-safe —
  defaults to `IsOrphaned = false`, `OrphanedAt = null`).
- `IRoleMetadataStore` gains a method. Implementers (only EF Core today)
  pick up one new method; unbreaking because it's additive.
- `SoftDelete` adds visible "ghost" rows in admin UIs until a cleanup
  pass. Mitigation: the upcoming admin endpoint lists orphans explicitly.

### Neutral

- Integration events (`RoleOrphanedEto`, `RoleMetadataHardDeletedEto`)
  join the existing `RoleCreatedEto` / `RoleUpdatedEto` / `RoleDeletedEto`
  family; downstream consumers opt-in by subscribing.

## Rollout

1. **This PR** (#1118): domain + store + per-provider option and sync
   wiring + unit tests. Default stays `KeepAndLog` — zero behaviour
   change for existing consumers.
2. **Follow-up**: admin endpoint `GET /admin/roles/orphans` and the
   two integration events (tracked under the same Phase 3 umbrella).
3. **Follow-up**: Postgres integration tests covering cascade and soft-delete
   durability (`RoleMetadataOrphanPostgresTests` in the Authorization.EFCore
   integration suite).

## References

- ADR-025 / ADR-026 / ADR-027 — Phase 2 client-role sync (the three
  provider implementations that this policy layers on top of).
- #1114 — RoleMetadata Phase 2b / 3 epic.
- #1118 — this ADR's implementing PR (story).
- Epic #1093 — the Phase 2 predecessor that introduced `RoleMetadata.ClientId`.
