# Granit.Mergeable.EntityFrameworkCore

EF Core orchestrator for the Granit merge primitive (`Granit.Mergeable`). Concrete
implementation of `IMergeService<TAggregate>` driving the full pipeline: idempotency
cache, locks, transaction, scalar-field application, scatter-gather across registered
`IReferenceRewriter<T>` participants, tombstone, chain-collapse, outbox-friendly
persistence.

## Contents

| Type | Purpose |
|---|---|
| `EfMergeService<TAggregate>` | Open-generic concrete `IMergeService<TAggregate>`. Registered by `AddGranitMergeableEntityFrameworkCore` and resolved per aggregate type by consuming modules. |
| `MergeableDbContext` | Isolated DbContext owning the `granit.merge_idempotency` cache table. |
| `MergeIdempotencyEntry` | Internal cache row. |
| `MergeableConcurrencyLock` | Postgres `pg_advisory_xact_lock` / SQL Server `sp_getapplock` helper, mirroring the `Granit.Metering` pattern. |
| `MergeRequestHasher` | Canonicalises a `MergeRequest` into a stable SHA-256 digest used as the replay key. |
| `MergeableEntityFrameworkCoreHostApplicationBuilderExtensions.AddGranitMergeableEntityFrameworkCore` | Wires the DbContext + the open-generic `EfMergeService`. |

## Pipeline

1. **Idempotency check** — same key + same canonical hash → replay cached `MergeResult`. Same
   key + different hash → 409 (key reused for a different intent).
2. **TransactionScope.Serializable** wraps every DbContext opened inside (single-Postgres
   assumption; no DTC).
3. **Per-tenant advisory lock** serialises concurrent merges on the same tenant.
4. **Load survivor + loser** via the consumer-supplied `IMergeableAggregateAdapter<T>` (which
   is responsible for bypassing the `IHasMergeTombstone` filter so the loser stays observable).
5. **Validate** — ids differ, neither already tombstoned, both alive.
6. **Compute conflicts** via `IMergeable.GetConflicts(loser)`.
7. **Dry-run** branch: rewriters use `CountAsync`, transaction rolls back. Used by the
   admin preview UI.
8. **Live** branch:
   - `survivor.MergeFrom(loser, choices)` applies scalar-field overrides.
   - Scatter-gather: each `IReferenceRewriter<T>` runs `RewriteAsync` (bulk SQL UPDATE)
     in the same transaction.
   - `adapter.ApplyTombstone(loser, survivor.Id, now)` + chain-collapse via
     `adapter.CollapseChainTombstonesAsync`.
   - `adapter.PersistMergedPairAsync(survivor, loser)` saves both inside the transaction.
   - Idempotency cache write (best-effort).
   - `transactionScope.Complete()`.

## Wiring

```csharp
builder.AddGranitMergeableEntityFrameworkCore(opt => opt.UseNpgsql(connectionString));

// Per consuming module (e.g. Granit.Parties.Mergeable):
services.AddScoped<IMergeableAggregateAdapter<Party>, EfPartyMergeableAggregateAdapter>();
services.AddReferenceRewriter<Party, InvoicePartyReferenceRewriter>();
// …
```

The consuming app then resolves `IMergeService<Party>` from DI and gets a fully wired
`EfMergeService<Party>`.

## Critical contracts

- The consumer's `IMergeableAggregateAdapter.LoadAsync` MUST disable the
  `IHasMergeTombstone` filter via `IDataFilter.Disable<IHasMergeTombstone>()` for the
  duration of the load so the orchestrator can read both survivor and loser even when
  the loser is already tombstoned by an earlier merge.
- The consumer's `ApplyTombstone` MUST set both `MergedIntoId` and `MergedAt`. The
  aggregate exposes private setters; the consumer routes through an internal mutation
  point — no reflection, no public API surface.
- Chain-collapse via `CollapseChainTombstonesAsync` runs `UPDATE … SET MergedIntoId =
  newSurvivor WHERE MergedIntoId = oldSurvivor` so a single hop is always enough for
  `ResolveCurrentAsync`.

## License

Apache 2.0.
