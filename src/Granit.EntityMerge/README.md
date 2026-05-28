# Granit.EntityMerge

Generic merge primitive for Granit aggregates. Lets a domain aggregate absorb a duplicate
(the **loser**) into the surviving instance, with per-field admin override at merge time
and pluggable cross-module reference rewriting.

## Why

Multi-channel data entry (CRM imports, ERP sync, signup forms) inevitably generates
duplicates. Without a merge tool, downstream aggregates corrupt: invoices issued against
the wrong party, ledger balances fragmented, provider mappings in conflict. This package
ships the framework-level primitive; first consumer is `Granit.Parties` (party
deduplication, [Epic granit-fx/granit-dotnet#1278](https://github.com/granit-fx/granit-dotnet/issues/1278)),
but the contract is aggregate-agnostic and reusable for catalog products, future
leads/opportunities, etc.

## Public surface

| Type | Role |
|---|---|
| `IHasMergeTombstone` (in `Granit`) | State-only contract: `MergedIntoId` + `MergedAt`. Decoupled from the merge behaviour so EF query filters, admin listings, and audit views can target it without depending on this package. |
| `IMergeable<TSelf>` | Aggregate marker carrying `GetConflicts(loser)` + `MergeFrom(loser, choices)`. |
| `IReferenceRewriter<TAggregate>` | Plug-in registry: each module that owns a foreign key to `TAggregate` registers one rewriter (`RewriteAsync` for live UPDATE, `CountAsync` for dry-run preview). |
| `IMergeService<TAggregate>` | Orchestrator contract — concrete EF impl ships in `Granit.EntityMerge.EntityFrameworkCore`. |
| `MergeRequest` / `MergeResult<T>` / `FieldConflict` | API records. Idempotency-key support baked in. |
| `MergeFieldChoices` (+ `MergeFieldChoicesBuilder`, `WinnerSide`) | Per-field admin override for conflict resolution at merge time (Salesforce/Dynamics-style preview). |
| `MergeException` | Translates to HTTP 422 at the endpoint boundary. |
| `AddReferenceRewriter<TAggregate, TRewriter>()` | DI helper for module authors to plug their rewriter in at startup. |
| `GranitEntityMergeModule` | Module marker (no service registration here — consumer modules wire their own rewriters). |

## Usage

### Aggregate side

```csharp
public sealed class Party : AuditedAggregateRoot, IMergeable<Party>
{
    public Guid? MergedIntoId { get; private set; }
    public DateTimeOffset? MergedAt { get; private set; }
    public string Name { get; private set; }
    // ...

    public IReadOnlyList<FieldConflict> GetConflicts(Party loser) =>
        // Compare scalar fields; return non-empty diffs with default winner.
        ...

    public void MergeFrom(Party loser, MergeFieldChoices choices)
    {
        // Apply per-field choices; fall back to defaults via choices.ResolveOrDefault(...)
        // NEVER touch child collections (Addresses, Emails, ...) — they are rewritten by
        // a registered IReferenceRewriter via SQL bulk-update on the shadow FK.
    }
}
```

### Module side (rewriter)

```csharp
internal sealed class InvoicePartyReferenceRewriter(IDbContextFactory<InvoicingDbContext> f)
    : IReferenceRewriter<Party>
{
    public string Description => "Invoice.PartyId";

    public Task<int> RewriteAsync(Guid survivor, Guid loser, CancellationToken ct) =>
        f.CreateDbContext().Invoices
            .Where(i => i.PartyId == loser)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.PartyId, survivor), ct);

    public Task<int> CountAsync(Guid survivor, Guid loser, CancellationToken ct) =>
        f.CreateDbContext().Invoices.CountAsync(i => i.PartyId == loser, ct);
}
```

### Wiring

```csharp
services.AddReferenceRewriter<Party, InvoicePartyReferenceRewriter>();
```

## Persistence convention

When the aggregate implements `IHasMergeTombstone`, `ApplyGranitConventions` (in
`Granit.Persistence.EntityFrameworkCore`) auto-applies:

- `MergedIntoId` (Guid?) + `MergedAt` (DateTimeOffset?) columns
- Index on `MergedIntoId`
- Named query filter `GranitFilterNames.MergeTombstone` excluding tombstoned rows
  (bypass via `IDataFilter.Disable<IHasMergeTombstone>()` for admin views)

No per-module mapping required.

## Critical design decisions

- **Scalar vs collection split** — `MergeFrom` only handles scalar fields. Child collections
  (HasMany shadow-FK) MUST be rewritten by a dedicated `IReferenceRewriter` via SQL bulk-update;
  in-memory `Collection.AddRange` would conflict with EF PK tracking.
- **Single-Postgres assumption** — the orchestrator wraps cross-module rewriters in a
  `TransactionScope` with `IsolationLevel.Serializable`. Single physical Postgres expected
  (no DTC).
- **Tombstone chain collapse** — when A→B is followed by B→C, the orchestrator updates
  `A.MergedIntoId` from B to C in the same transaction. A single hop always resolves a stale
  id to the current survivor.
- **Idempotency multi-layer** — Stripe-style `IdempotencyKey` on `MergeRequest`; replay-safe
  via DB UNIQUE constraint on `(survivorId, loserId, requestHash)` plus the HTTP middleware
  layer.

## License

Apache 2.0 — same as the rest of the Granit framework.
