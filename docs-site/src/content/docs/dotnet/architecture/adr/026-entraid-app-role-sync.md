---
title: "ADR-026: Entra ID App Role distinction and boot-time sync"
description: "Apply the ADR-025 client-role template to Microsoft Entra ID via the Graph API"
sidebar:
  order: 26
  label: "026 - Entra ID app roles"
---

> **Date:** 2026-04-22
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Identity.Federated.EntraId`

## Context

ADR-025 introduced the `IIdentityClientRoleManager` capability interface, the
additive `IdentityRole.ClientId` property, and the
`IHostDataSeedContributor`-driven sync pipeline that upserts `RoleMetadata`
rows with a non-null `ClientId`. Keycloak was the first implementer.

Microsoft Entra ID exposes an equivalent concept — **App Roles** declared on
an application registration, surfaced at runtime on the associated **Service
Principal** in the tenant. Assignments live on
`users/{id}/appRoleAssignments` scoped by `resourceId` (the Service Principal
object id). This ADR plugs Entra into the ADR-025 template.

## Decision

### Apply the ADR-025 template 1-for-1

`EntraIdIdentityProvider` now implements `IIdentityClientRoleManager` in
addition to `IIdentityProvider`. `SupportsClientRoles` on
`EntraIdIdentityProviderCapabilities` returns `true`. Nothing about the
interface shape, the DIM, or the `IdentityRole.ClientId` field changes — this
ADR records the Entra-specific bindings only.

### Two-step resolution: appId → Service Principal object id

Entra has two distinct GUIDs for the same app:

- **`appId`** — the OIDC `client_id` (stable across tenants for multi-tenant
  apps; the one configured in `KeycloakAdmin:ClientRoleSync:TrackedAppIds`).
- **Service Principal `id`** — the object id of the instantiation of the
  application in the tenant (used in every Graph URL that touches app roles
  or assignments).

`GetClientRolesAsync` and `GetUserClientRolesAsync` therefore issue a
resolution call first:

```text
GET /v1.0/servicePrincipals
    ?$filter=appId eq '{appId}'
    &$select=id,appId,appRoles
```

The payload carries both `id` (object id) and the inline `appRoles` array, so
`GetClientRolesAsync` returns without a second round-trip. `GetUserClientRolesAsync`
re-uses the resolved object id in:

```text
GET /v1.0/users/{userId}/appRoleAssignments?$filter=resourceId eq {sp.id}
```

Unknown `appId` → `EntraIdClientNotFoundException` — the sibling of
`KeycloakClientNotFoundException`. The sync catches it and logs a Warning.

### Disabled App Roles are filtered out

Entra App Roles carry an `isEnabled` flag (admins can deprecate a role
without deleting it). `GetClientRolesAsync` filters to `isEnabled = true` —
we only mirror active roles into `RoleMetadata`. Assignments targeting a
disabled role are silently dropped by `GetUserClientRolesAsync` because the
role is not in the active role map.

### Configuration — `EntraIdClientRoleSyncOptions`

Bound to `EntraIdAdmin:ClientRoleSync`. Same shape as Keycloak, but the
tracked list is `appId` GUIDs rather than human client ids:

```json
{
  "EntraIdAdmin": {
    "ClientRoleSync": {
      "Enabled": true,
      "TrackedAppIds": [
        "11111111-1111-1111-1111-111111111111"
      ]
    }
  }
}
```

Explicit opt-in is important: Entra tenants host thousands of Service
Principals — Microsoft built-ins (Office 365, Teams, Defender...), other
SaaS integrations — none of which are relevant here. Enumerating all of them
would flood `RoleMetadata`.

### DI — single scoped instance shared across facets

```csharp
services.AddIdentityProvider<EntraIdIdentityProvider>();  // scoped
services.TryAddScoped<IIdentityClientRoleManager>(sp =>
    sp.GetRequiredService<IIdentityProvider>() as IIdentityClientRoleManager
    ?? throw new InvalidOperationException("..."));
```

Same pattern as ADR-025; locked by unit test
`IdentityProvider_And_ClientRoleManager_ResolveToSameScopedInstance`. Sharing
matters because `EntraIdAdminTokenService` (singleton) caches the admin
token — doubling the provider would double the token-acquisition traffic and
split internal scoped state.

### Failure policy — log & skip (identical to ADR-025)

Sync never crashes host startup. Per-app failures:

- Graph unreachable (`HttpRequestException` / `TaskCanceledException`) → log Warning, continue.
- 403 Forbidden on `/servicePrincipals` → log **Error** with the required
  Graph permissions (`Application.Read.All`, `AppRoleAssignment.ReadWrite.All`),
  continue.
- `EntraIdClientNotFoundException` (tracked appId not in the tenant) → log Warning, continue.

## Required Microsoft Graph application permissions

The admin client configured via `EntraIdAdmin:ClientId` / `ClientSecret`
needs the following **Application** permissions (not Delegated), granted with
admin consent:

| Permission | Purpose |
| ---------- | ------- |
| `Application.Read.All` | List Service Principals and project their `appRoles`. |
| `AppRoleAssignment.ReadWrite.All` | Read user App Role assignments (phase 2a); also covers future assignment writes. |

Existing `EntraIdAdminOptions` remarks already document the broader permission
set required by the provider — this ADR adds no new permission on top.

## Consequences

### Positive

- Entra App Roles now round-trip cleanly through the authorization engine
  just like Keycloak client roles.
- `Granit.Identity.Federated.EntraId` stays aligned with the ADR-025 template —
  Cognito (#1100) will drop in the same way.
- Disabled App Roles are never written to `RoleMetadata`, so Entra admins can
  deprecate roles without orphaning grants immediately (grants on a
  now-disabled role remain but lookups stop resolving — matches Entra's own
  runtime behavior).

### Negative

- `Granit.Identity.Federated.EntraId` gains project references to
  `Granit.Authorization`, `Granit.Guids`, and `Granit.Persistence.EntityFrameworkCore`
  to host the sync service — same trade-off as ADR-025.
- An extra Graph call per tracked app at boot (`servicePrincipals?$filter=appId eq ...`).
  Acceptable given the opt-in model and the low cardinality of the tracked list.

## References

- ADR-025 — Keycloak client-role sync (the template this ADR follows).
- #1099 — this ADR's implementing PR.
- [Microsoft Graph — servicePrincipal resource type](https://learn.microsoft.com/graph/api/resources/serviceprincipal)
- [Microsoft Graph — appRoleAssignment resource type](https://learn.microsoft.com/graph/api/resources/approleassignment)
