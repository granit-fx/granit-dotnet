---
title: "ADR-037: Party merge framework — Mergeable primitive + cross-module rewriters"
description: "Generic merge framework (Granit.Mergeable) plus a per-module IReferenceRewriter<T> registry to fold a duplicate Party into a survivor across Invoicing / Subscriptions / CustomerBalance, with override-at-merge-time field choices, single-Postgres TransactionScope orchestration, and a tombstone follow-through pattern for in-flight references."
sidebar:
  order: 37
  label: "037 - Party Merge Framework"
---

> **Date:** 2026-04-27
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit` (`IHasMergeTombstone`), `Granit.Persistence.EntityFrameworkCore`
> (filter + column convention), `Granit.Mergeable`,
> `Granit.Mergeable.EntityFrameworkCore`, `Granit.Parties` (`Party : IMergeable<Party>`),
> `Granit.Parties.Mergeable` (adapter + Party-children rewriter),
> `Granit.Parties.Endpoints` (admin endpoints + audit writer),
> `Granit.Invoicing.EntityFrameworkCore`,
> `Granit.Subscriptions.EntityFrameworkCore`,
> `Granit.CustomerBalance.EntityFrameworkCore` (inlined rewriters)

## Context

Multi-channel data entry (CRM import, tenant signup, ERP sync, sales form) inevitably
produces duplicate `Party` rows: the same customer created twice, a supplier mapped via
two different external providers, an individual contact and their employer captured as
two separate companies. Without a merge primitive, those duplicates accumulate and
corrupt every consuming aggregate that references the `PartyId`:

- `Granit.Invoicing` issues invoices against the wrong party.
- `Granit.Subscriptions` keeps the billing cycle pinned to a stale party.
- `Granit.CustomerBalance` fragments the ledger across two parties for a single customer.
- `Granit.Parties.ExternalMappings` ends up with conflicting Stripe / Mollie / Odoo ids
  for the same human entity.

A merge feature is **generic enough to be a framework primitive** rather than a
Parties-specific concern: the same shape will apply to a future `Catalog.Product`
deduplication, to `Lead` / `Opportunity` consolidation in a CRM module, etc. Detection
of duplicates is a separate concern (statistical pipeline, see Epic
[#1300](https://github.com/granit-fx/granit-dotnet/issues/1300)) and is delivered as
its own Epic.

The plan was driven by prior-art research on Salesforce, HubSpot, Dynamics 365,
Odoo `res.partner._merge`, SAP Business Partner, NetSuite, Stripe, MDM tooling
(Reltio, Informatica), and the standard string-matching algorithm family
(Jaro-Winkler, Levenshtein, Metaphone, Fellegi-Sunter, LSH).

## Decision

### 1. Two complementary contracts: state vs behavior

The framework splits the merge contract into a **state-only** marker and a
**behavior + state** interface, both living in `Granit`:

```csharp
public interface IHasMergeTombstone
{
    Guid? MergedIntoId { get; }          // null on survivor; survivor.Id on loser
    DateTimeOffset? MergedAt { get; }
}

public interface IMergeable<TSelf> : IHasMergeTombstone
    where TSelf : Entity, IMergeable<TSelf>
{
    void MergeFrom(TSelf loser, MergeFieldChoices choices);
}
```

- **`IHasMergeTombstone`** is the marker that EF Core, query filters, listings, audit,
  and a future un-merge endpoint can target without knowing anything about the merge
  service. `ApplyGranitConventions` detects it and auto-applies a `MergedIntoId` /
  `MergedAt` column pair, an index on `MergedIntoId`, and a named query filter
  `GranitFilterNames.MergeTombstone` that excludes tombstoned rows by default — the
  same shape as `ISoftDeletable`. Adding tombstone behavior to a future aggregate is
  a one-line interface change with zero migration plumbing per module.
- **`IMergeable<TSelf>`** is the operational contract: an aggregate that can absorb a
  loser into itself given a per-field choice spec. Constraint
  `where TSelf : Entity, IMergeable<TSelf>` keeps the contract usable by both
  `AggregateRoot` and `AuditedAggregateRoot` (parallel hierarchies share the
  `Entity` base) without forcing a single root type.

### 2. Override-at-merge-time field choices (Salesforce / Dynamics-style)

Survivorship is decided **per merge call**, not declaratively up-front:

```csharp
public sealed class MergeFieldChoices
{
    public IReadOnlyDictionary<string, WinnerSide> Choices { get; init; }
}

public enum WinnerSide { Survivor, Loser }

public sealed record FieldConflict(
    string FieldPath,
    object? SurvivorValue,
    object? LoserValue,
    WinnerSide Default);
```

The admin previews the merge, sees a side-by-side diff with a `Default` recommendation
per conflicting field, picks `Survivor` or `Loser` per row, and submits. The chosen
map ends up in the audit log as `ResolvedChoices` JSON — fully traceable.

This is rejected from MDM-style declarative survivorship rules (Reltio / Informatica)
because:

- Auditability suffers: a centrally-configured rule blob is opaque on a per-merge
  basis, whereas `ResolvedChoices` records the literal decisions that were applied.
- Coverage is the wrong shape: a 4-field rule cannot anticipate every conflict in a
  20-field aggregate; the override-at-merge-time form forces the operator to confront
  every conflict explicitly.
- Cost / benefit: typing-rules infrastructure pays off only above a few hundred
  merges/day. We expect single-digit merges/day per tenant in v1.

If MDM-style rules become necessary later, they can be layered on top by feeding
`MergeFieldChoices` from a configuration source — the runtime contract does not need
to change.

### 3. Strict pair (1 survivor + 1 loser) — chain N>2 manually

A merge takes exactly two parties: one survivor, one loser. Three-way (or N-way)
merges are out of scope:

- The preview UX is already a side-by-side diff. A 3×N matrix preview is
  combinatorially harder to operate.
- The chain `merge(A→B); merge(B→C)` is not equivalent in audit terms to a single
  `merge({A,B}→C)`, but the chain is what an operator would naturally do anyway
  (compare each pair head-to-head).
- Chain-collapse (see §6 below) ensures that the first-step loser tombstone is
  rewritten in the second step, so a single `ResolveCurrent` hop always suffices —
  the chain leaves no stale tombstones behind.

### 4. Plug-in registry of cross-module rewriters

Each module that persists a `PartyId` (or a `Party.ParentContactId` self-FK) ships an
`IReferenceRewriter<Party>` whose responsibility is to rewrite its own foreign key
in bulk:

```csharp
public interface IReferenceRewriter<TAggregate> where TAggregate : Entity
{
    string Description { get; }                               // "Invoice.PartyId" — surfaced in the audit log
    Task<int> RewriteAsync(Guid survivorId, Guid loserId, CancellationToken ct);
    Task<int> CountAsync(Guid survivorId, Guid loserId, CancellationToken ct);  // dry-run
}
```

The orchestrator (`EfMergeService<TAggregate>`) discovers all registered rewriters via
DI and fan-out scatters them inside a single transaction (see §5).

`RewriteAsync` performs a single SQL `UPDATE` via `ExecuteUpdateAsync` rather than
loading entities into the change tracker. On 100 k rows of `Invoice.PartyId`, the
EF-tracked path would burn millions of round-trips and exhaust memory; the bulk-SQL
path completes in sub-second on a warm connection.

#### 4.a No `*.Mergeable` per-module package

The first iteration of the plan called for a dedicated `Granit.{Module}.Mergeable`
package per concerned module (Invoicing, Subscriptions, CustomerBalance, …). That
shape was rejected after the third package: each `*.Mergeable` ended up containing a
single internal class (~50 lines of SQL) plus a csproj, a module class, a DI
extension, a test project, and CI shard wiring (≈10 plumbing files for 50 lines of
work). Multiplied by every future module that gains a typed FK to `Party`, the
maintenance overhead dominated the value.

**Decision**: the rewriter for `{Module}.{Property}` lives **inlined inside the
module's existing `*.EntityFrameworkCore` package**, registered from its existing
`AddGranit{Module}EntityFrameworkCore(...)` extension. The architecture test
`MergeableConventionTests` enforces the placement (rewriter assembly must be a
`*.EntityFrameworkCore` or a `*.Mergeable` — both shapes accepted, but new modules
default to inlining).

`Granit.Parties.Mergeable` remains as a dedicated package because it owns more than
one rewriter and a non-trivial aggregate adapter (see §7) — that volume earns its
own assembly.

### 5. Single-Postgres `TransactionScope` orchestration (no DTC)

The orchestrator (`EfMergeService<TAggregate>` in `Granit.Mergeable.EntityFrameworkCore`)
runs the merge inside one `TransactionScope` at `IsolationLevel.Serializable`:

1. **Idempotency precheck** — if the request carries an `Idempotency-Key`, look up
   `merge_idempotency` (key, request_hash, result_blob, created_at). On a hit with
   matching hash → return the cached result. On a hit with a different hash → 409
   (key reused for a different request). Entries kept 24 h.
2. **Pre-lock validation** — tenant match, kind match, default-currency match, neither
   archived, neither already merged.
3. **Open `TransactionScope` with `IsolationLevel.Serializable`** wrapping every
   subsequent step.
4. **Take a tenant-wide advisory lock** —
   `pg_advisory_xact_lock(hashtext('party-merge-' || tenantId))` (mirrors
   `Granit.Metering.EntityFrameworkCore.MeteringConcurrencyLock`). Auto-released on
   COMMIT/ROLLBACK. Serializes concurrent merges within a tenant; merges across
   tenants stay parallel.
5. **`SELECT … FOR UPDATE`** on both Party rows and re-validate (re-read
   `MergedIntoId == null` on both, re-read `RowVersion`).
6. **Compute `FieldConflict` list** via the aggregate adapter (see §7) — same call
   path as preview.
7. **If `DryRun`**: the rewriters are called via their `CountAsync` overload
   (`SELECT COUNT(*)` rather than `UPDATE`). The TransactionScope is disposed
   without `Complete()` → state pre-merge is intact. `MergeResult` is returned with
   `Conflicts` + `RewriteCounts`.
8. **If live merge**:
   - `survivor.MergeFrom(loser, choices)` applies the scalar field choices.
   - All registered `IReferenceRewriter<Party>` instances are iterated. Each opens
     its own `DbContext` but enrolls in the ambient `TransactionScope` via
     `Connection.EnlistTransaction` — Npgsql does this automatically when the same
     connection-string targets the same physical Postgres.
   - The aggregate adapter applies the tombstone (`MergedIntoId = survivor.Id`,
     `MergedAt = clock.Now`, `Status = Archived`) and **collapses any prior chain**
     (`UPDATE parties SET MergedIntoId = newSurvivor WHERE MergedIntoId = oldSurvivor`).
   - `SaveChangesAsync` then `transactionScope.Complete()`.
   - Outside the scope: best-effort UPSERT into `merge_idempotency`.
9. **Outbox** — `PartyMergedEto` is enqueued via the Wolverine outbox in the same
   SaveChanges, giving downstream consumers (cache invalidation, search index,
   materialized views) at-least-once delivery.

#### Why `TransactionScope` and not separate `IDbContextTransaction` per module

The merge legitimately spans 4+ DbContexts (Parties + Invoicing + Subscriptions +
CustomerBalance). Coordinating them with explicit per-context transactions would
require either:

- a saga (commit each context, compensate on partial failure) — viable but the
  compensation paths are non-trivial (uncommit a `PartyId` rewrite is itself a
  rewrite), or
- a 2-phase commit / DTC — broken on Linux, ruled out.

**Single-Postgres assumption.** The Granit deployment standard runs every module's
DbContext against the same physical PostgreSQL cluster (logical isolation via
schemas / tables, not physical isolation). Under that assumption,
`TransactionScope` with `IsolationLevel.Serializable` enrolls every connection
against the same physical transaction — no DTC required, and an exception in any
rewriter rolls back the entire merge.

The assumption is documented at the framework level. A deployment that splits
modules across multiple Postgres clusters would require the saga form; that form is
out of scope for v1 and would be delivered as `Granit.Mergeable.Wolverine` if a
real customer requested it.

### 6. Tombstone follow-through for in-flight references

The bulk-`UPDATE` rewriters cover **persisted typed FK columns** (`Invoice.PartyId`,
`Subscription.PartyId`, `BalanceAccount.PartyId`, `Party.ParentContactId`).
A `PartyId` can also live in places SQL cannot rewrite cleanly:

| Category | Location | Why bulk-SQL cannot rewrite it |
| --- | --- | --- |
| Wolverine messages in flight | `wolverine_outbox`, `wolverine_inbox` (JSON payload) | Schema-less; the `PartyId` may sit at any depth |
| Scheduled background jobs | `scheduling_deferred_actions`, `wolverine_durable_messages` | Same |
| Pending webhook deliveries | `webhook_envelopes` | Same |
| Queued notifications | `notifications_pending` | Same |
| Already-published integration events | External brokers | Out of our control |
| Audit log entries | `audit_entries.EntityId` | **MUST NOT** rewrite — audit is immutable history |

**Pattern: tombstone follow-through.** Any consumer that receives a `PartyId`
resolves it on receipt:

```csharp
public static class PartyIdTombstoneExtensions
{
    /// <summary>
    /// Resolves a (possibly stale) PartyId to its current survivor by following the
    /// MergedIntoId tombstone. No-op if the party is alive. A single hop suffices —
    /// chain merges (A→B→C) are collapsed at merge time so MergedIntoId always
    /// points to the final survivor.
    /// </summary>
    public static async ValueTask<PartyId> ResolveCurrentAsync(
        this PartyId partyId,
        IPartyReader reader,
        IDataFilter dataFilter,
        CancellationToken ct);
}
```

**Chain collapse at merge time.** When `merge(survivor=B, loser=A)` runs, then later
`merge(survivor=C, loser=B)`, the orchestrator rewrites `A.MergedIntoId` from `B` to
`C` in the same transaction (`UPDATE parties SET MergedIntoId = C WHERE MergedIntoId = B`).
Consequence: a single `ResolveCurrentAsync` hop always reaches the final survivor;
no recursion, no cycle risk.

**Audit log UX.** The query layer joins on `parties.MergedIntoId` to render
"Party X (merged into Y on …)" without rewriting `AuditEntry.EntityId`.

A future architecture-test story
([#1409](https://github.com/granit-fx/granit-dotnet/issues/1409)) will assert that
every Wolverine handler / background job whose signature carries a `PartyId` calls
`ResolveCurrentAsync`. That check requires either a Roslyn analyzer or an IL-level
scan (NetArchTest sees method signatures, not method bodies) — deferred from the
initial framework PR.

### 7. Per-aggregate adapter for module-specific concerns

Most of the merge orchestration is generic across `TAggregate`, but each aggregate
needs a small bridge:

```csharp
public interface IMergeableAggregateAdapter<TAggregate> where TAggregate : Entity, IMergeable<TAggregate>
{
    Task<TAggregate?> LoadAsync(Guid id, CancellationToken ct);
    Task PersistAsync(TAggregate survivor, TAggregate loser, CancellationToken ct);
    Task ApplyTombstoneAsync(Guid survivorId, Guid loserId, CancellationToken ct);
    Task CollapseChainAsync(Guid oldSurvivorId, Guid newSurvivorId, CancellationToken ct);
}
```

`PartyMergeableAggregateAdapter` (in `Granit.Parties.Mergeable`) implements the four
operations against `PartiesDbContext`, plus the special-case
`PartyChildrenReferenceRewriter` that bulk-rewrites the shadow FK on the
Addresses / Emails / Phones / ExternalMappings child rows (cap-enforced) — a
per-aggregate concern that does not generalize.

### 8. Audit at endpoint level (for now)

The post-merge audit entry composes one `AuditEntry` with a single `AuditEntityChange`
on the survivor and four `AuditPropertyChange` rows: `MergedFromId` (loser id),
`Reason` (operator-supplied), `ResolvedChoices` (JSON map), `RewriteCounts` (JSON map).

It is currently written **from the endpoint, after the orchestrator commits** —
not from an `ILocalEventHandler<PartyMergedEvent>`. The handler-based path is the
clean target architecture, but it requires `Party.RaiseMergedEvents` to be wired
into the orchestrator's SaveChanges, which is pending. Writing from the endpoint
ships the audit need today and is a mechanical migration once the handler path lands
(no schema change).

The audit write is post-commit and best-effort — an audit-DB outage at this exact
moment loses the audit entry while keeping the merge. That is the standard trade-off
the framework already accepts for post-commit handlers; rolling back a successful
merge because the audit DB is unreachable is worse than missing one entry.

## Consequences

### Positive

- **Generic merge primitive.** The same `Granit.Mergeable` framework will absorb
  `Catalog.Product`, future `Lead` / `Opportunity`, etc. with a per-module rewriter
  that is ~50 lines of SQL.
- **Auto-applied tombstone column convention.** Adding `IHasMergeTombstone` to a new
  aggregate is one interface line; column / index / query filter are auto-emitted by
  `ApplyGranitConventions`. No copy-paste of mapping code per module.
- **Bulk-SQL rewrite scales to high cardinality.** `ExecuteUpdateAsync` on
  `Invoice.PartyId` completes sub-second on 100 k rows; the EF-tracked alternative
  was unviable.
- **Two-layer idempotency.** HTTP-level (`Granit.Http.Idempotency` middleware) plus
  DB-level (`merge_idempotency` UNIQUE on `(survivorId, loserId, requestHash)`)
  protects against an SDK that regenerates its `Idempotency-Key` between retries.
- **Concurrent merges are correctly serialized.** Tenant-wide advisory lock plus
  `SELECT … FOR UPDATE` on the two Party rows yields a deterministic 409 to the
  losing concurrent caller.
- **Architecture test catches drift.** `MergeableConventionTests` reflects over
  loaded assemblies and asserts that every aggregate carrying a typed `PartyId`
  outside `Granit.Parties` has a registered `IReferenceRewriter<Party>` in either
  its `*.EntityFrameworkCore` or its `*.Mergeable` assembly.

### Negative

- **Single-Postgres assumption is load-bearing.** A deployment splitting modules
  across multiple Postgres clusters would need a saga-based variant
  (`Granit.Mergeable.Wolverine`) that does not exist yet. Documented; deferred to v2.
- **Audit timing is endpoint-level rather than event-driven** until
  `Party.RaiseMergedEvents` is wired into the orchestrator. Functionally equivalent
  but mechanically duplicated (the endpoint composes the audit entry inline).
  Tracked as future work.
- **Stale `PartyId` in JSON payloads is the consumer's problem.** Wolverine
  handlers, scheduled jobs, webhook envelopes, and queued notifications must call
  `ResolveCurrentAsync` themselves. Until the architecture test
  ([#1409](https://github.com/granit-fx/granit-dotnet/issues/1409)) lands, the
  enforcement is convention-only and a missing call yields silent data routing to
  a tombstoned party.
- **No un-merge.** A successful merge is reversible only by manual data surgery in
  v1 (loser is soft-archived with the tombstone, but the rewriters are not undone).
  An explicit `POST /parties/{id}/unmerge` (best-effort, restore loser, do **not**
  re-rewrite refs) is tracked as
  [#1304](https://github.com/granit-fx/granit-dotnet/issues/1304) at P3.

### Non-goals

- **No external-system propagation.** The loser's `ExternalMappings` are archived
  with the loser; cleanup on Stripe / Mollie / Odoo is left to a human in the loop.
  Provider APIs are either irreversible or non-existent for this operation.
- **No hard delete.** Soft-archive plus `MergedIntoId` is the chosen reversibility
  story; hard delete would lose the audit chain.
- **No cross-tenant merge.** Hard-blocked by the multi-tenancy invariant.
- **No same-currency `BalanceAccount` consolidation in v1.** The DB UNIQUE
  constraint on `(PartyId, Currency)` rejects the merge when both parties already
  have a balance in the same currency. Consolidation (sum the two ledgers) is
  tracked as tech-debt
  [#1402](https://github.com/granit-fx/granit-dotnet/issues/1402) and will land via
  a generic `IMergeConsolidator<T>` once a second use case appears.

## References

- ADR-017 — DDD aggregate root and value object strategy
- ADR-022 — Module naming (no technology suffix on domain modules)
- Epic [#1278](https://github.com/granit-fx/granit-dotnet/issues/1278) — Party
  merge + duplicate detection
- Feature [#1279](https://github.com/granit-fx/granit-dotnet/issues/1279) —
  Mergeable framework + Party domain + cross-module rewriters + endpoints + UI
- Tech debt [#1402](https://github.com/granit-fx/granit-dotnet/issues/1402) —
  `IMergeConsolidator<T>` for same-currency BalanceAccount consolidation
- Tech debt [#1409](https://github.com/granit-fx/granit-dotnet/issues/1409) —
  Architecture check that handlers / jobs accepting `PartyId` call
  `ResolveCurrentAsync`
