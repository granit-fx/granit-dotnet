---
title: "ADR-025: Keycloak client-role distinction and boot-time sync"
description: "Introduce IIdentityClientRoleManager, the Keycloak implementation, and the boot-time sync that populates RoleMetadata.ClientId"
sidebar:
  order: 25
  label: "025 - Keycloak client roles"
---

> **Date:** 2026-04-22
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Identity`, `Granit.Identity.Federated.Keycloak`

## Context

Phase 1 introduced `RoleMetadata.ClientId` (nullable, indexed with
`(Name, TenantId, ClientId)` unique under `NULLS NOT DISTINCT`) **reserved**
for client-scope roles from federated providers. No provider populated the
field until now — the store treated every role as realm-scope.

Keycloak supports two native role families:

- **Realm roles** (global inside the realm) — already surfaced by Granit's
  `IIdentityRoleManager` via `GET /admin/realms/{realm}/roles`.
- **Client roles** (scoped to a specific OIDC client inside the realm) — this
  ADR adds them.

The abstraction introduced here is the template for the Entra ID app roles
(#1099) and Cognito app-client-bound groups (#1100) follow-ups.

## Decision

### `IIdentityClientRoleManager` — a separate capability interface

Rather than extending the 5-method `IIdentityRoleManager` (inherited by
`IIdentityProvider` and implemented by 4 other providers today), a new
**capability interface** `IIdentityClientRoleManager` is added alongside it.
Only providers that support the concept natively implement it; AspNet Identity
— which has no analog — does not, and stays untouched. The interface is *not*
inherited by `IIdentityProvider`.

Shape:

```csharp
Task<IReadOnlyList<string>>       GetClientsAsync(CancellationToken);
Task<IReadOnlyList<IdentityRole>> GetClientRolesAsync(string clientId, CancellationToken);
Task<IReadOnlyList<IdentityRole>> GetUserClientRolesAsync(string userId, string clientId, CancellationToken);
```

`IIdentityProviderCapabilities.SupportsClientRoles` surfaces the capability as
a DIM (default false). Keycloak overrides to `true`. Consumers can guard their
logic without reflecting on service registrations.

### Additive `ClientId` on `IdentityRole`

`IdentityRole` gains a `string? ClientId { get; init; }` non-positional
property. Positional constructor stays `(Id, Name, Description)` so
existing callers and positional deconstruction are unchanged. Client-scope
results carry the client id; realm results leave it `null`.

### Sync pipeline — `IHostDataSeedContributor`

`KeycloakClientRoleSyncContributor` implements `IHostDataSeedContributor` and
runs `KeycloakClientRoleSyncService.SyncAsync` at host boot. Mirrors the
`IdentityLocalRoleSeedContributor` precedent; runs once per host boot in a
tenantless scope.

**Idempotent upsert** via `(Name, TenantId, ClientId)` unique index:

- `FindByNameAsync(name, tenantId: null, clientId)` → not found → `Create + AddAsync`.
- Found with description drift → `Rename` + `UpdateAsync` (covers description-only change; client role name renames would collide with the unique index so Keycloak itself wouldn't allow them).
- Found unchanged → no-op.

**No orphan deletion** in this phase. A client role removed in Keycloak leaves
its `RoleMetadata` row intact — deleting it cascades onto `PermissionGrant`
rows and can silently strip permissions from active users. A future
`RemoveOrphanedRoles` flag can opt in once we have an event-driven
reconciliation or admin-triggered audit.

### Configuration — `KeycloakClientRoleSyncOptions`

Dedicated options class bound to `KeycloakAdmin:ClientRoleSync`:

```json
{
  "KeycloakAdmin": {
    "ClientRoleSync": {
      "Enabled": true,
      "TrackedClientIds": ["showcase-admin", "showcase-patient"]
    }
  }
}
```

Explicit opt-in on `TrackedClientIds` keeps the sync out of Keycloak's
internal clients (`admin-cli`, `security-admin-console`, `account`, etc.)
which have no application relevance.

### DI — single scoped instance shared across facets

Registered in `AddGranitIdentityKeycloak`:

```csharp
services.AddIdentityProvider<KeycloakIdentityProvider>(); // scoped, existing
services.TryAddScoped<IIdentityClientRoleManager>(sp =>
    sp.GetRequiredService<IIdentityProvider>() as IIdentityClientRoleManager
    ?? throw new InvalidOperationException("..."));
```

Both interfaces resolve to the same scoped `KeycloakIdentityProvider` — locked
down by unit test `IdentityProvider_And_ClientRoleManager_ResolveToSameScopedInstance`.
Critical because `KeycloakAdminTokenService` (singleton) is shared and
doubling the provider instance would double the admin token acquisition and
split internal state.

### Failure policy — log & skip

Sync never crashes host startup. Per-client failures:

- Keycloak unreachable (`HttpRequestException` / `TaskCanceledException`) → log Warning, continue.
- 403 Forbidden on `view-clients` → log **Error** with the required `realm-management`
  roles (`view-clients`, `query-clients`, `view-realm`), continue.
- `KeycloakClientNotFoundException` (tracked client id not in the realm) → log Warning, continue.

Next boot = natural retry. For production hardening a scheduled job or
healthcheck-gated sync can follow in Phase 3.

## Required Keycloak service-account roles

The admin client configured via `KeycloakAdmin:ClientId` / `ClientSecret` needs
the following realm-management roles:

- `view-clients` (list + resolve client UUIDs)
- `query-clients` (filter by `clientId`)
- `view-realm` (read realm metadata)

Add from the Keycloak admin console: *Clients → {admin-client} → Service Accounts
Roles → Assign role → realm-management:* pick the three above.

## Consequences

### Positive

- Non-breaking for every existing identity provider.
- Client roles are now first-class in the authorization engine:
  `IGranitRoleLookup.FindByNameAsync(name, clientId: "app-a")` returns a
  distinct row, separate from any same-named realm role.
- Abstraction shape is ready-made for Entra app roles (#1099) and Cognito
  app-client-bound groups (#1100).

### Negative

- `Granit.Identity.Federated.Keycloak` gains project references to
  `Granit.Authorization`, `Granit.Guids`, and `Granit.Persistence.EntityFrameworkCore`
  to host the sync service. Could be extracted to a
  `Granit.Identity.Federated.Keycloak.Authorization` package later if the
  coupling becomes painful.
- Duplicate role names across clients (`admin` on `app-a` AND on `app-b`)
  coexist correctly in the data layer but appear identical in admin UIs until
  the frontend dropdown is updated to disambiguate (tracked as a frontend
  follow-up in the PR description).

## References

- #1082 — Phase 1 `RoleMetadata` + `ClientId` reservation.
- #1098 — this ADR's implementing PR.
- ADR-023 — `IGranitRoleLookup.FindByNameAsync(name, clientId?)` already
  accepts the `clientId` parameter.
- [Keycloak Admin REST API — Clients](https://www.keycloak.org/docs-api/latest/rest-api/index.html)
