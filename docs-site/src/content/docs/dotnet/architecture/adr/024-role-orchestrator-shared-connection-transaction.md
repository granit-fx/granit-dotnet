---
title: "ADR-024: Shared-connection EF Core transaction for role orchestration"
description: "Upgrade IGranitRoleOrchestrator from compensating-write to atomic shared-connection transaction when both DbContexts target the same database"
sidebar:
  order: 24
  label: "024 - Role orchestrator atomic transaction"
---

> **Date:** 2026-04-22
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Identity.Local.AspNetIdentity`, `Granit.Authorization.EntityFrameworkCore`, `Granit.OpenIddict.EntityFrameworkCore`, `Granit.Persistence.EntityFrameworkCore`

## Context

`IGranitRoleOrchestrator` dual-writes every role lifecycle operation across two
independent DbContexts: `GranitRole` lands in the Identity DbContext (typically
`OpenIddictDbContext`) and `RoleMetadata` lands in the host application's DbContext
(which implements `IPermissionGrantDbContext`). Phase 1 (PR #1082) used a
**compensating-write** strategy — write Identity first, then metadata; on metadata
failure delete the orphaned Identity role — because no portable atomic mechanism
was available:

- `TransactionScope` would escalate to MSDTC on Linux with Npgsql, which is not
  supported.
- EF Core's `Database.OpenConnectionAsync` / `SetDbConnection` / `BeginTransactionAsync` /
  `UseTransactionAsync` pattern requires the Identity DbContext to expose its
  `DbConnection` across assemblies — but `OpenIddictDbContext` is `internal sealed`.

The compensating strategy leaves a non-atomic window: a process crash between the
Identity INSERT and the metadata INSERT produces an orphan `GranitRole` that the
seed contributor's repair path has to clean up on next startup. The risk is
low-impact but real.

This ADR records the Phase 2 upgrade (PR #1097): opt in to a **true atomic EF
Core transaction** when the deployment satisfies the pre-conditions, while keeping
the compensating path as the fallback for deployments that don't.

## Decision

### Accessor pattern (not `public sealed` OpenIddictDbContext)

Two new interfaces in `Granit.Persistence.EntityFrameworkCore.SharedConnection`:

- `IIdentityDbContextAccessor` — exposes the scoped Identity `DbContext` (same
  instance ASP.NET Identity's `RoleStore` / `UserStore` use). Implemented by
  `OpenIddictIdentityDbContextAccessor` in `Granit.OpenIddict.EntityFrameworkCore`.
- `IAuthorizationHostDbContextAccessor` — factory-style; `CreateFreshDbContextAsync`
  returns a freshly-built host `DbContext` via `IDbContextFactory<THost>`.
  Implemented by `AuthorizationHostDbContextAccessor<TContext>` in
  `Granit.Authorization.EntityFrameworkCore`.

Both are registered automatically by the existing `AddGranitOpenIddict(...)` and
`AddGranitAuthorizationEntityFrameworkCore<TContext>()` extensions — no host-app
code change required.

**Rationale for the accessor pattern instead of making `OpenIddictDbContext` public
sealed:** `OpenIddictDbContext` extends `IdentityDbContext<GranitUser, GranitRole, Guid>`
with a substantial public API surface (dozens of inherited members, multiple generic
constraints). Exposing it publicly commits the framework to that shape. The
accessor interface is 1 member, purpose-built, and easy to evolve.

### Asymmetry: scoped Identity, factory host

The Identity accessor returns the scoped instance because ASP.NET Identity's
`RoleManager<GranitRole>` internally references a `RoleStore<GranitRole, OpenIddictDbContext, Guid>`
whose `TContext` was resolved by DI at construction time. If the orchestrator
handed the RoleManager a different DbContext, `RoleManager.CreateAsync` would
write to the "wrong" context — not enrolled in the shared transaction. Staying
scoped keeps RoleManager and the accessor pointing at the same instance.

The host accessor, in contrast, returns a **fresh** context per call. The
orchestrator swaps its underlying `DbConnection` (via `Database.SetDbConnection`)
to share the Identity connection. Calling `SetDbConnection` on a DbContext that
has already executed queries in the scope ("warm context") can throw
`InvalidOperationException` on EF Core 8 / 9 / 10. A freshly-built context is
guaranteed not to be warm, so the swap is always safe.

Consequence: the orchestrator's atomic path **bypasses `IRoleMetadataStore`**
inside the transaction block and writes directly via
`freshHostCtx.Set<RoleMetadata>().Add(...)` + `SaveChangesAsync`. The store is
tied to the *scoped* host context, which is not the one participating in the
shared transaction. The duplicated logic is 2 lines (Add + SaveChanges); no
meaningful redundancy.

### Provider-agnostic connection-string comparison

Connection-string equivalence is checked via
`System.Data.Common.DbConnectionStringBuilder.EquivalentTo(DbConnectionStringBuilder)`
— the ADO.NET base-class API that parses key/value pairs regardless of order and
ignores whitespace. Works for every ADO.NET provider (Npgsql, SQL Server, SQLite,
MySQL, etc.). Wrapped in `try/catch`: a malformed connection string throws, and
the catch falls back to compensating-write.

### Retry strategy compatibility

Both supported providers unconditionally enable `EnableRetryOnFailure(3, 30s)`
(see `NpgsqlDbContextOptionsExtensions` and `SqlServerDbContextOptionsExtensions`).
Calling `Database.BeginTransactionAsync` while retry is active throws
`InvalidOperationException` unless wrapped in
`Database.CreateExecutionStrategy().ExecuteAsync(...)`. The atomic path does so;
`ChangeTracker.Clear()` runs at the start of each attempt so a re-execution
starts from clean tracker state.

Transient errors (network blips, serialization failures) trigger a retry of the
whole block — both contexts re-execute from scratch. Non-transient errors
(unique-index violations, application exceptions) rethrow after `RollbackAsync`
and the transaction dies cleanly.

## Consequences

### Positive

- Atomic dual writes when both contexts target the same database. A crash
  mid-operation leaves zero orphan rows.
- No breaking change: applications not registering the accessors continue on the
  compensating-write path, behavior unchanged from Phase 1.
- Provider-agnostic (PostgreSQL tested end-to-end via Testcontainers; SQL Server
  and other relational providers supported by the same API).
- No `TransactionScope`, no MSDTC, no ambient transaction shenanigans.

### Negative

- Graceful-fallback complexity: the orchestrator now has two execution paths to
  maintain. Mitigated by keeping them in clearly-separated private methods.
- Requires the host DbContext to be registered via `AddDbContextFactory<T>`
  (which `AddGranitDbContext` does). Apps using `AddDbContext<T>` alone don't
  benefit from the atomic path — but they still work, via the compensating
  fallback.
- `ILogger` noise: per-call `Debug` messages document which path was selected.
  Deployments that want to audit this can raise the log level or suppress the
  category.

## Pre-conditions for the atomic path

At runtime, the orchestrator takes the atomic path iff **all** of:

1. `IIdentityDbContextAccessor` is registered.
2. `IAuthorizationHostDbContextAccessor` is registered AND
   `IDbContextFactory<THost>` resolves successfully.
3. Both resolved DbContexts report `Database.IsRelational() == true`.
4. Their connection strings are equivalent (`DbConnectionStringBuilder.EquivalentTo`).

Otherwise: compensating-write.

## Providers covered

| Provider | Retry enabled by Granit | Pattern validated | Integration test |
| -------- | ----------------------- | ----------------- | ---------------- |
| PostgreSQL (Npgsql) | `EnableRetryOnFailure(3, 30s)` | Yes | Testcontainers PG 17 |
| SQL Server (`Microsoft.Data.SqlClient`) | `EnableRetryOnFailure(3, 30s)` | Same EF Core API | Follow-up (no Granit integration test exercises SQL Server on this path yet) |
| Other relational providers (SQLite, MySQL via Pomelo, etc.) | Out of Granit's direct support | Covered by the `IExecutionStrategy` abstraction | Not covered |

## Interceptor interaction

Granit's EF interceptors (`AuditedEntityInterceptor`, `DomainEventDispatcherInterceptor`,
`SoftDeleteInterceptor`, etc.) run through the standard `SaveChanges` pipeline,
which operates on the context's current connection and transaction. They do not
care that `SetDbConnection` swapped the underlying connection. `DomainEventDispatcherInterceptor`
may publish integration events via `IDistributedEventBus` in `SavingChanges` —
these writes happen on their own DbContext and are NOT enrolled in the shared
transaction. This is consistent with the outbox pattern's existing behavior and
is not a regression from Phase 1.

## References

- PR #1082 — Phase 1 `RoleMetadata` + compensating-write orchestrator.
- PR #1097 — this ADR's implementing PR.
- [Microsoft docs — Sharing connection and transaction](https://learn.microsoft.com/en-us/ef/core/saving/transactions#share-connection-and-transaction)
