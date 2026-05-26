# Granit.Indexing.Privacy

GDPR Art. 17 bridge between `Granit.Privacy` and any registered indexing backend.

## What's in this package

- `PersonalDataDeletionHandler` — Wolverine handler that subscribes to `PersonalDataDeletionRequestedEto` and fans the request to every registered `IIndexedDataEraser`.
- `GranitIndexingPrivacyModule` — Granit module wiring; `[DependsOn]` declares `GranitIndexingModule` + `GranitPrivacyModule`.

## How the cascade works

1. A data subject requests deletion (`PersonalDataDeletionRequestedEto` reaches the bus).
2. The handler resolves every `IIndexedDataEraser` from DI (one per backend — `Granit.Indexing.EntityFrameworkCore` ships `EfIndexedDataEraser`).
3. Each eraser bulk-deletes rows in its backend filtered by `(TenantId, DataSubjectId)` — a single `ExecuteDelete()` for the EF backend, `delete_by_query` for Elasticsearch, etc.
4. Erasers are idempotent: Wolverine retries and manual replays converge safely.

## Why a separate package

`Granit.Indexing.EntityFrameworkCore` is provider-pure (Postgres tsvector storage, nothing else). Adding the Wolverine handler here keeps the `Granit.Privacy` dependency confined to this one bridge — applications that don't need GDPR cascade (single-tenant, internal-only) can skip this package entirely.

## Usage

```csharp
// In your composition root, after the indexing backend(s):
builder.Services.AddGranitIndexing();
builder.Services.AddGranitIndexingEntityFrameworkCore(opts => opts.UseNpgsql(cs), typeof(Guid));

// Pull in the cascade (registers the Wolverine handler):
modules.Register<GranitIndexingPrivacyModule>();
```

Producers populate `IndexedEntry<TKey>.DataSubjectId` from `IIndexedEntrySource<TKey>.GetDataSubjectIdAsync` so the cascade has the FK to filter on.
