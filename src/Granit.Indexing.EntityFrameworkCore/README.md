# Granit.Indexing.EntityFrameworkCore

Postgres `tsvector` storage backend for `Granit.Indexing` — the default consumer-host pairs against the framework's full-text search abstraction.

## What's in this package

- `IndexingDbContext` — multi-tenant DbContext extending `GranitDbContext`. One physical table per registered `TKey`.
- `IndexedEntryRow<TKey>` — entity with `Content`, `SearchVector tsvector` (Postgres `GENERATED ALWAYS … STORED`), `Language`, `Summary`, `Tags string[]`, `IsTruncated`, `CharCount`, `DataSubjectId`.
- `HasGeneratedTsVectorColumn(...)` extension method (Postgres-scoped) — emits the generated column + GIN index. Lives here, **not** in `Granit.Persistence.EntityFrameworkCore` (provider-neutral).
- `EfIndexer<TKey>` — upsert by `(TenantId, Key)`, idempotent, raises local domain events.
- `EfSearchBackend<TKey, TResult>` — plugs into the Story 1 orchestrator; `Granit.Indexing` exposes the `ISearchService<TKey, TResult>` to consumers.
- `PersonalDataDeletionHandler` — Wolverine handler subscribed to `PersonalDataDeletionRequestedEto`. Cascades GDPR Art. 17 deletion to every indexed row tied to the subject in a single `ExecuteDelete()` per registered `TKey`.

## Security gates

### tsquery injection (CWE-89 sub-case)

Default search path uses `plainto_tsquery` (or `websearch_to_tsquery` via `IndexingEntityFrameworkCoreOptions.UseWebSearchSyntax`). Both parse caller input as natural language; operator characters (`&`, `|`, `!`, parentheses) are treated as literals. `to_tsquery` is intentionally NOT reachable from this path — a future advanced-search backend gated by a `Search.Advanced.Execute` permission on the consumer's permission tree may opt callers into it.

### GDPR Art. 17 (right to be forgotten)

Indexed copies must not outlive their source. Hosts that index personal data populate `IndexedEntry<TKey>.DataSubjectId` (via `IIndexedEntrySource<TKey>.GetDataSubjectIdAsync`); `PersonalDataDeletionHandler` then erases everything tied to the subject when the deletion request reaches the bus.

### Tenant isolation

Inherited from `GranitDbContext`: the parameterised tenant filter is rewritten into every SQL command at execution time (no closure-leak risk). Indexed entries cannot be read across tenants.

## Usage

```csharp
builder.Services.AddGranitIndexing();
builder.Services.AddGranitIndexingEntityFrameworkCore(
    opts => opts.UseNpgsql(connectionString),
    typeof(Guid));

builder.Services.AddGranitIndexingBackend<Guid, MyHitResponse>(
    row => new MyHitResponse(row.Key, row.Summary ?? string.Empty, row.Tags));
```

## Migrations

This package ships no EF migrations. Consumer hosts own them.

```bash
dotnet ef migrations add InitIndexing \
    --context IndexingDbContext \
    --project YourHost/YourHost.csproj
```
