---
title: "ADR-031: Client-role write operations on IIdentityClientRoleManager"
description: "Extend the read-only Phase 2 IIdentityClientRoleManager with CreateClientRoleAsync / AssignClientRoleAsync / RemoveClientRoleAsync so admin UIs can author client-scoped roles directly, using each provider's native admin surface"
sidebar:
  order: 31
  label: "031 - Client-role writes"
---

> **Date:** 2026-04-23
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Identity`, `Granit.Identity.Federated.Keycloak`,
> `Granit.Identity.Federated.EntraId`, `Granit.Identity.Federated.Cognito`

## Context

Phase 2 (ADR-025 / ADR-026 / ADR-027) introduced `IIdentityClientRoleManager`
with three read-only methods — `GetClientsAsync`, `GetClientRolesAsync`,
`GetUserClientRolesAsync` — plus a boot-time sync that mirrored upstream
client roles into `RoleMetadata`. Phase 3 (ADR-029 orphan cleanup,
ADR-030 scheduled re-sync) closed the drift gap but kept the model
**upstream-authoritative**: an admin had to log into Keycloak / Azure /
AWS consoles to author or grant a role, and Granit mirrored the result.

For Granit-first deployments — where the showcase admin UI is the
intended source of truth and upstream IaC is either absent or driven
**by** Granit — that loop is painful: the admin UI can display roles
but cannot create, grant or revoke them. This ADR turns the provider
abstraction into a **write surface** so the admin UI can stay the only
pane of glass.

## Decision

### Interface extension, not a new interface

`IIdentityClientRoleManager` gains three methods — `CreateClientRoleAsync`,
`AssignClientRoleAsync`, `RemoveClientRoleAsync`. Alternatives considered:

1. **New `IIdentityClientRoleWriter` interface** — explicit separation
   of reads vs writes, but forces callers to inject two interfaces for
   a single conceptual capability and doubles the DI registration.
2. **Chosen:** extend the existing interface. All three federated
   providers already have the natural implementation; there is no
   provider that implements reads but cannot implement writes today.
   A capability flag (`SupportsClientRoleWrites`) handles opt-out.

### Capability flag for opt-out

`IIdentityProviderCapabilities.SupportsClientRoleWrites` is added as a
DIM defaulting to `false`. Each federated provider overrides to `true`.
Providers that want to remain read-only (e.g. a future SCIM-backed
provider where writes go through a different channel) can keep the
default and throw `NotSupportedException` from the write methods —
callers that respect the flag never reach those paths.

Concretely, endpoints that expose these operations should guard on the
flag before dispatching:

```csharp
if (!capabilities.SupportsClientRoleWrites)
{
    return TypedResults.Problem(
        "Provider does not support client-role writes.",
        StatusCodes.Status501NotImplemented);
}
```

### Per-provider implementation choices

Each provider maps the three operations to its native admin surface.
The abstraction is intentionally thin — there is no provider-neutral
"role lifecycle event" emitted from the provider layer; event
emission is the responsibility of callers (endpoints, the admin UI
service, the orchestrator) that know the domain context.

#### Keycloak

Direct HTTP calls against the Keycloak Admin REST API:

- **Create** — resolve the client UUID, then
  `POST /admin/realms/{realm}/clients/{uuid}/roles` with a
  `KeycloakRoleRepresentation` where `ContainerId = {uuid}` and
  `ClientRole = true`. Refetch via
  `GET /admin/realms/{realm}/clients/{uuid}/roles/{name}` to obtain
  the server-assigned id (Keycloak returns a 201 with no body).
- **Assign** — resolve the client UUID, fetch the role by name, then
  `POST /admin/realms/{realm}/users/{userId}/role-mappings/clients/{uuid}`
  with the single role representation.
- **Remove** — same resolution, then `DELETE` on the same endpoint.

Required service-account permissions:
`realm-management: manage-clients, manage-users, view-clients`.

#### Entra ID (Microsoft Graph)

App Roles live on the **Application** object, not the Service
Principal. The implementation:

- **Create** — `GET /v1.0/applications?$filter=appId eq '{clientId}'`
  to resolve the application object id + existing `appRoles` array,
  then `PATCH /v1.0/applications/{objectId}` with
  `{ "appRoles": [...existing, newRole] }`. New role id is minted
  locally via `IGuidGenerator` (Graph does not assign ids for inline
  app roles).
- **Assign** — resolve the Service Principal (via `appId`),
  `POST /v1.0/users/{userId}/appRoleAssignments` with
  `{ principalId, resourceId = spObjectId, appRoleId }`.
- **Remove** — resolve SP, list user's `appRoleAssignments` filtered
  by the SP's object id, find the assignment with the matching
  `appRoleId`, then `DELETE` it. No-op if no match (matches the
  interface contract).

**Concurrency caveat:** the create path is **not concurrency-safe**.
Two parallel `CreateClientRoleAsync` calls on the same application
will both read the pre-mutation `appRoles` array, both PATCH their
own delta, and whichever lands second wins — the first role is lost.
Graph does not expose per-role insert / `ETag` precondition semantics
for `appRoles`. This is acceptable for the showcase admin UI
(single-admin authoring) but must be documented in the operator
README. Mitigation for high-concurrency deployments: gate calls at
the orchestrator layer with a per-`appId` lock, or move to upstream
IaC for role authoring and keep only assign / remove writes.

Required Graph permissions:
`Application.ReadWrite.All` (for create),
`AppRoleAssignment.ReadWrite.All` (for assign / remove).

#### AWS Cognito

Cognito has no native client-scoped roles — Granit uses the
`{appClientId}{Delimiter}{roleName}` group-naming convention from
ADR-027 to infer scope. Writes mirror reads:

- **Create** — `CreateGroup` with the prefixed name.
- **Assign** — `AdminAddUserToGroup` with the prefixed name.
- **Remove** — `AdminRemoveUserFromGroup` with the prefixed name.

The delimiter is read from `CognitoClientRoleSyncOptions.Delimiter`
(default `:`), so create and sync stay consistent by construction.

Required IAM:
`cognito-idp:CreateGroup`, `cognito-idp:AdminAddUserToGroup`,
`cognito-idp:AdminRemoveUserFromGroup`.

### Eventing and audit

No domain / integration events are emitted from the provider layer
for these operations. Rationale:

- The provider layer is not the right surface for domain events —
  callers (endpoints, orchestrator) know the domain context
  (tenant, initiating admin, audit purpose) and already have access
  to the event bus.
- The existing realm-role `AssignRoleAsync` / `RemoveRoleAsync`
  methods on `IIdentityProvider` do publish `IdentityRoleAssignedEto`
  / `IdentityRoleRemovedEto`. Those are kept as-is. For client-role
  writes, if a future story needs a parallel event (`IdentityClientRoleAssignedEto`),
  it can be added without re-opening this ADR — the abstraction is
  already the right shape.

### Observability

All six new code paths (3 methods × 3 providers) start a dedicated
`Activity` via each provider's `ActivitySource`:

- Keycloak: `client_role.create`, `client_role.assign`,
  `client_role.remove`.
- Entra ID: `client_role.create`, `client_role.assign`,
  `client_role.remove`.
- Cognito: `client_role.create`, `client_role.assign`,
  `client_role.remove`.

Tags: `user_id`, `client_id` where relevant. This matches the
existing naming pattern for read operations (`GetClientRoles` /
`GetUserClientRoles`).

## Consequences

### Positive

- Granit-first deployments can close the admin UI loop for client
  roles — create / grant / revoke without leaving the Granit pane.
- All three federated providers reach parity on the write surface;
  downstream code can rely on `SupportsClientRoleWrites` as a
  uniform check.
- The provider abstraction is now symmetric with the realm-role
  surface on `IIdentityProvider` (`AssignRoleAsync` /
  `RemoveRoleAsync`) — less cognitive load for new contributors.

### Negative

- Entra ID create is inherently not concurrency-safe. Documented
  in the per-method remarks and in this ADR; accepted trade-off for
  v1. Callers expecting high-concurrency authoring need to serialize
  at the orchestrator layer.
- New IAM / Graph / Keycloak permissions are required. Deployments
  upgrading from Phase 2 must grant
  `Application.ReadWrite.All`, `manage-clients`, or `CreateGroup`
  (depending on provider) before enabling the admin UI features
  that call these methods.

### Neutral

- The `IGuidGenerator` dependency added to
  `EntraIdIdentityProvider` formalizes a rule already in force
  (GRSEC002 bans `Guid.NewGuid()`). All tests inject
  `SimpleGuidGenerator`; production hosts get the tenant-aware
  generator through `AddGranitGuids`.

## Rollout

1. **This PR** (#1122): interface extension, 3 provider
   implementations, 9 unit tests (3 × 3), this ADR. No endpoint
   changes — the admin-UI endpoint work is a separate frontend /
   backend PR pair.
2. **Follow-up (admin UI endpoints)**: `Granit.Identity.Endpoints`
   will expose POST / DELETE routes that dispatch to the provider
   via the orchestrator, guarded by `SupportsClientRoleWrites`.
3. **Follow-up (orchestrator-level serialization)**: if the Entra
   concurrency caveat bites in practice, introduce a per-`appId`
   lock at the orchestrator layer. Not shipped here to avoid
   premature complexity.

## References

- ADR-025 / ADR-026 / ADR-027 — Phase 2 read-only client-role sync.
- ADR-029 — Orphan cleanup policy.
- ADR-030 — Scheduled re-sync.
- #1114 — RoleMetadata Phase 2b / 3 epic.
- #1122 — this ADR's implementing PR (story).
