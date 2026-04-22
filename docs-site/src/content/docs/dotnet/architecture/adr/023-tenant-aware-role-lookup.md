---
title: "ADR-023: Tenant-aware role lookup"
description: "Universal ILookupNormalizer registration and IGranitRoleLookup abstraction for multi-tenant role resolution"
sidebar:
  order: 23
  label: "023 - Tenant-aware role lookup"
---

> **Date:** 2026-04-22
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Identity.Local.AspNetIdentity`, `Granit.Identity.Local`

## Context

`RoleMetadata` (introduced in Phase 1 — ADR-020 references it) carries a
declarative `MultiTenancySide` scope so a single display name like `Manager` can
exist both as a host role and, independently, as a tenant role inside each
tenant. ASP.NET Core Identity's `AspNetRoles.NormalizedName` column, however, is
indexed with a **process-wide** unique constraint — two tenants cannot both have
a `Manager` entry under the default `UpperInvariantLookupNormalizer`.

Phase 1 shipped `TenantAwareRoleLookupNormalizer` but deliberately left it
unregistered because two design questions were unresolved:

1. `ILookupNormalizer.NormalizeName` is invoked for both `NormalizedUserName`
   and `NormalizedName` — should we prefix user names too, or find a way to
   apply the prefix only to roles?
2. `Both`-scope roles (`TenantAdministrator`, `User`) are stored with
   `TenantId = null` and therefore with an un-prefixed `NormalizedName`. Queried
   from a tenant context, the prefixed lookup misses them. How do callers
   discover these globally-defined roles from inside a tenant?

This ADR pins down the answers.

## Decision

### 1. Register `TenantAwareRoleLookupNormalizer` universally

`GranitIdentityLocalAspNetIdentityModule` replaces the default
`ILookupNormalizer` with `TenantAwareRoleLookupNormalizer` unconditionally
(scoped lifetime). The prefix therefore also applies to
`GranitUser.NormalizedUserName`, with the following semantics:

| Context of creation | Context of lookup | Found? |
| ------------------- | ----------------- | ------ |
| Host | Host `FindByNameAsync` | ✅ |
| Tenant A | Tenant A `FindByNameAsync` | ✅ |
| Tenant A | Tenant B `FindByNameAsync` | ❌ (invisible) |
| Tenant A | Host `FindByNameAsync` | ❌ (invisible) |
| Host | Tenant A `FindByNameAsync` | ❌ (invisible) |

This matches Granit's tenant-isolation intent: host and tenant administrative
contexts never share a user-by-name lookup in practice. Email-based lookups
(`NormalizeEmail`) stay upper-invariant so cross-context login by email still
works for the rare case of a platform-level service hitting a tenant user via
email.

**Rejected alternative**: flag the behavior behind
`RoleEndpointsOptions.AllowTenantRoles`. That ties a platform-wide
infrastructure concern (ASP.NET Identity indexing) to an endpoints-specific
flag, and produces silent semantic changes the day the flag flips. The
universal registration is simpler and the user-name side effect is benign in
practice (see table above).

### 2. Introduce `IGranitRoleLookup`

A new abstraction (`Granit.Identity.Local.Services.IGranitRoleLookup`) is the
canonical lookup path for business code. It routes through `IRoleMetadataStore`
(the authoritative catalog) rather than `RoleManager.FindByNameAsync`, and
implements the tenant-aware fallback:

1. If a tenant context is active, try the tenant-scope row
   (`TenantId = currentTenant.Id`).
2. Otherwise — or on a miss — try the host-scope row (`TenantId = null`),
   which covers both `Host` and `Both` roles.

Other-tenant rows are never returned. `ClientId` is propagated through both
probes to leave room for the Phase 2 realm/client role distinction
(#1098 – #1100).

`RoleManager<GranitRole>.FindByNameAsync` is reserved for the one legitimate
Identity-table use case: the orphan-repair path in
`IdentityLocalRoleSeedContributor` that detects a `GranitRole` left over from a
partial seed without matching `RoleMetadata`.

## Consequences

### Positive

- Same display name usable across tenants without leaking through the global
  unique index.
- Business flows consistently see roles through one lens (`IGranitRoleLookup`),
  independent of the normalizer strategy.
- `Both`-scope roles (seeded platform-wide) stay discoverable from every tenant
  via the host-scope fallback.

### Negative

- Users created in a tenant context are not findable by `UserManager.FindByNameAsync`
  from the host context (and vice-versa). Applications that need cross-context
  lookup must use `FindByEmailAsync` or query `GranitUser` directly through the
  DbContext with an explicit `TenantId` filter.
- The `TenantAwareRoleLookupNormalizer` is scoped, which costs a per-request
  allocation compared to the singleton `UpperInvariantLookupNormalizer`. The
  overhead is dominated by the scoped `ICurrentTenant` resolution that already
  exists on every request path, so net impact is negligible.

## Migration

Phase 1 callers of `RoleManager.FindByNameAsync` for business lookups: none
found in the framework source at the time of this ADR. New callers must use
`IGranitRoleLookup`; reviewers should flag direct `RoleManager.FindByNameAsync`
usage outside of the orphan-repair path.

## References

- #1087 — RoleMetadata Phase 1 integration tests (orchestrator + endpoints).
- #1094 — Phase 2 story landing this ADR.
- ADR-020 — Declarative Query/Export definitions placement.
