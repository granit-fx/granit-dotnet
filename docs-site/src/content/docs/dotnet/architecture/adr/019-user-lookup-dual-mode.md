---
title: "ADR-019: User Lookup Dual Mode — Cache vs Local Store"
description: "UserCacheEntry is a caching implementation detail for external providers, not a domain concept. IUserLookupService abstracts both modes."
---

## Status

Accepted

## Context

Granit supports two identity provider modes:

1. **External provider** (Keycloak, Entra ID, Cognito, Google Cloud) — users
   live in an external system that cannot be queried via SQL. A local
   `UserCacheEntry` table mirrors user data for search, display names, and
   audit log resolution.

2. **Local provider** (OpenIddict / ASP.NET Core Identity) — users live in the
   application database as `GranitUser`. They are already queryable via SQL.

With the introduction of `Granit.OpenIddict` (#383), the `UserCacheEntry`
table and `UserCacheSyncMiddleware` become redundant in local mode:

- `GranitUser` IS the source of truth — no cache needed.
- The sync middleware performs unnecessary writes on every authenticated request.
- Three user tables (`GranitUser` + `UserCacheEntry` + app `UserProfile`)
  create data duplication and desynchronization risk.

## Decision

### `IUserLookupService` is the single abstraction

All framework modules (`AuditLog`, `Notifications`, `Timeline`, etc.) consume
users exclusively through `IUserLookupService`. Two implementations exist:

| Mode | Implementation | Data source |
|------|---------------|-------------|
| External provider | `CachedUserLookupService` | `UserCacheEntry` (SQL cache) |
| Local provider | `AspNetIdentityUserLookupService` | `GranitUser` (direct SQL) |

The active implementation is determined by which identity provider package is
registered (`Granit.Identity.Keycloak` vs `Granit.Identity.OpenIddict`).

### `IIdentityProviderCapabilities.IsLocalStore`

A new `IsLocalStore` property on `IIdentityProviderCapabilities` indicates
whether users are stored locally. This property drives:

- **`UserCacheSyncMiddleware`** — short-circuits when `IsLocalStore = true`
- **`Granit.Identity.EntityFrameworkCore`** — not required when `IsLocalStore = true`

### `UserCacheEntry` is a caching concern

`UserCacheEntry` is an **implementation detail** of `CachedUserLookupService`,
not a first-class domain concept:

- No module outside `Granit.Identity.EntityFrameworkCore` references it directly.
- It is not created when the identity provider is local (OpenIddict).
- It exists solely to enable SQL queries against external provider user data.

## Consequences

### Positive

- No redundant database table in OpenIddict mode.
- No wasteful per-request sync middleware in OpenIddict mode.
- Clean abstraction — consumers of `IUserLookupService` work unchanged.
- `IIdentityProviderCapabilities.IsLocalStore` enables future optimizations
  (e.g., skip background stale refresh when local).

### Negative

- Adding `IsLocalStore` to the interface requires updating all existing
  provider capability implementations (5 files — one-time cost).

### Neutral

- Application-level `UserProfile` (preferences, avatar) remains the
  application's responsibility in both modes.
