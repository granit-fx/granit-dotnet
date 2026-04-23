---
title: "ADR-027: Cognito app-client group sync via naming-prefix convention"
description: "Apply the ADR-025 client-role template to AWS Cognito User Pools using a {appClientId}:role naming prefix, since Cognito has no native group-to-app-client binding"
sidebar:
  order: 27
  label: "027 - Cognito app-client groups"
---

> **Date:** 2026-04-23
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Identity.Federated.Cognito`

## Context

ADR-025 introduced the `IIdentityClientRoleManager` capability interface, the
additive `IdentityRole.ClientId` property, and the `IHostDataSeedContributor`
sync pipeline. Keycloak (#1098 / ADR-025) and Entra ID (#1099 / ADR-026)
implemented it against native provider constructs — Keycloak client roles,
Entra App Roles. This ADR plugs AWS Cognito User Pools into the same
template, closing the Phase 2 story #1100.

AWS Cognito does **not** offer a native "client role" concept. The available
primitives are:

- **Groups** — a flat list per User Pool. Each group carries a name,
  a description, a precedence, and an optional IAM Role ARN (used only by
  **Cognito Identity Pools** for AWS credential federation, not by User Pool
  app clients).
- **App Clients** — OIDC clients registered in a User Pool. No link to groups
  is exposed by the Cognito API.

The "group bound to a specific app client" concept mentioned in #1100 is
therefore a **Granit-level convention**, not a Cognito feature.

## Decision

### Naming-prefix convention: `{appClientId}{Delimiter}{roleName}`

A Cognito group whose name matches `{TrackedAppClientId}{Delimiter}{RoleName}`
is treated as a client-scoped role. `{Delimiter}` defaults to `":"` and can
be overridden via `CognitoClientRoleSyncOptions.Delimiter`. The stripped
`{RoleName}` is what lands in `IdentityRole.Name` and `RoleMetadata.Name`;
`{appClientId}` lands in `IdentityRole.ClientId` and `RoleMetadata.ClientId`.

**Why a naming prefix (not description tags, not external mapping):**

- **Infrastructure as truth** — a DevOps engineer opening the Cognito console
  sees `orders-api:admin` and immediately understands the scope. No second
  source of config.
- **No split-brain** — an external `appsettings` mapping would let admins
  create a group in Cognito that the Granit config forgets to mention
  (or vice-versa). Prefixes eliminate that class of drift.
- **Resilient** — descriptions are free-form strings that Terraform scripts,
  admin notes, or automation routinely overwrite.
- **Aligned with common practice** — tenants that already namespace their
  Cognito groups for other reasons (per-product, per-environment) can adopt
  this convention by setting `Delimiter` to whatever separator they use.

### Un-prefixed groups stay on the realm path

Cognito's existing `GetRolesAsync` (via `IIdentityRoleManager`) already
enumerates **every** group as a realm-equivalent `IdentityRole`. The role
orchestrator consumes that output to write `RoleMetadata` rows with
`ClientId = null`.

The client-role sync introduced here does **not** touch un-prefixed groups —
processing them would double-write and collide on the
`(Name, TenantId, ClientId)` unique index for same-named realm rows. Strict
filtering is the only correct default; there is no "lenient mode".

### `IIdentityClientRoleManager` on `CognitoIdentityProvider`

- `GetClientsAsync` → `ListUserPoolClientsAsync` (paginated), projects every
  app client id in the User Pool. Caller (sync service) filters against
  `TrackedAppClientIds`.
- `GetClientRolesAsync(appClientId)` → `ListGroupsAsync` (paginated),
  keeps groups whose name starts with `{appClientId}{Delimiter}`, strips
  the prefix, returns as `IdentityRole` with `ClientId = appClientId`.
- `GetUserClientRolesAsync(userId, appClientId)` → `AdminListGroupsForUserAsync`
  (paginated), same prefix filter.

The provider reads the delimiter from `IOptions<CognitoClientRoleSyncOptions>` —
it is the single place where Granit's naming convention is encoded.
`SupportsClientRoles` on `CognitoIdentityProviderCapabilities` returns
`true`.

### Configuration — `CognitoClientRoleSyncOptions`

Bound to `CognitoAdmin:ClientRoleSync`:

```json
{
  "CognitoAdmin": {
    "ClientRoleSync": {
      "Enabled": true,
      "Delimiter": ":",
      "TrackedAppClientIds": [
        "ordersApiClient",
        "customerPortalClient"
      ]
    }
  }
}
```

Opt-in on `TrackedAppClientIds` — the sync never walks groups for untracked
clients. Default `Delimiter` is `":"`.

### DI — single scoped instance shared across facets

```csharp
services.AddIdentityProvider<CognitoIdentityProvider>();  // scoped
services.TryAddScoped<IIdentityClientRoleManager>(sp =>
    sp.GetRequiredService<IIdentityProvider>() as IIdentityClientRoleManager
    ?? throw new InvalidOperationException("..."));
```

Same pattern as ADR-025 / ADR-026. Locked by
`IdentityProvider_And_ClientRoleManager_ResolveToSameScopedInstance`.

### Failure policy — log & skip

- `NotAuthorizedException` from AWS → log **Error** with the required IAM
  actions (`cognito-idp:ListGroups`, `cognito-idp:AdminListGroupsForUser`,
  `cognito-idp:ListUserPoolClients`), continue.
- Other `AmazonCognitoIdentityProviderException` → log Warning, continue.

The host never crashes on sync failure; the next boot is a natural retry.

## Required AWS IAM actions

The IAM principal configured on the Cognito SDK client (`AccessKeyId` /
`SecretAccessKey` or implicit IAM role) needs the following actions on the
target User Pool:

| Action | Purpose |
| ------ | ------- |
| `cognito-idp:ListUserPoolClients` | `GetClientsAsync` — enumerate app clients. |
| `cognito-idp:ListGroups` | `GetClientRolesAsync` — enumerate groups and prefix-filter. |
| `cognito-idp:AdminListGroupsForUser` | `GetUserClientRolesAsync` — per-user group membership. |

These are additive to the existing Cognito admin permissions already required
by `CognitoIdentityProvider` (`ListUsersAsync`, `AdminGetUserAsync`, etc.)

## Consequences

### Positive

- Cognito reaches feature parity with Keycloak and Entra for the Phase 2
  client-role picture — `RoleMetadata.ClientId` is now populated for all
  three federated providers.
- Naming prefix keeps Cognito as the single source of truth; no split-brain
  between Cognito state and Granit configuration.
- The ADR-025 DI template ports cleanly, so future providers will drop in the
  same way.

### Negative

- `Granit.Identity.Federated.Cognito` gains project references to
  `Granit.Authorization`, `Granit.Guids`, and
  `Granit.Persistence.EntityFrameworkCore` to host the sync service — same
  trade-off as ADR-025 and ADR-026.
- Existing customers whose Cognito groups already use a colon in their names
  for another purpose (rare, but legal in Cognito — group names accept
  Unicode punctuation) must override `Delimiter` before enabling the sync.
- Cognito does not expose "active vs disabled" flags on groups the way Entra
  App Roles do (`isEnabled`). A group that needs to be retired must be
  deleted or renamed out of the prefix scope — there is no half-state.

## References

- ADR-025 — Keycloak client-role sync (template).
- ADR-026 — Entra ID App Role sync (parallel implementation).
- #1100 — this ADR's implementing PR.
- [AWS Cognito — Adding groups to a user pool](https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-user-pools-user-groups.html)
- [AWS Cognito — `ListGroups`](https://docs.aws.amazon.com/cognitoidentityprovider/latest/APIReference/API_ListGroups.html)
