# Granit.Indexing.Elasticsearch

Opt-in Elasticsearch 9.x backend for `Granit.Indexing`. Replaces the default EF/tsvector
implementations with a BM25 multi-field search over per-language analyzers.

## Why use it

The `Granit.Indexing.EntityFrameworkCore` package is the default — it ships zero infrastructure
beyond Postgres and is enough for most modular monoliths. Reach for Elasticsearch when:

- The corpus exceeds what a single Postgres tsvector index can comfortably serve (low-millions
  of rows or multi-GB content).
- Per-language analyzers / synonym maps / phrase scoring are core to UX.
- You already operate an Elasticsearch cluster and want to consolidate full-text workloads.

## Registration

```csharp
builder.Services.AddGranitIndexing();

builder.Services.AddGranitIndexingElasticsearch(
    configureClient: null,
    typeof(Guid));

builder.Services.AddGranitIndexingElasticsearchBackend<Guid, MyResponse>(
    keyProjection: doc => Guid.Parse(doc.Key),
    resultProjection: doc => new MyResponse(doc.Key, doc.Summary, doc.Tags));
```

`AddGranitIndexingElasticsearch` strips any previously registered `IIndexer<TKey>` and
`IIndexedDataEraser` to guarantee the host runs a single backend. Likewise,
`AddGranitIndexingElasticsearchBackend` replaces any prior `ISearchBackend<TKey, TResult>`.

## Configuration

`appsettings.json`:

```json
{
  "Indexing": {
    "Elasticsearch": {
      "Uri": "https://es.internal:9200",
      "ApiKey": "your-api-key",
      "Strategy": "Shared",
      "IndexPrefix": "granit-indexing",
      "BulkBatchSize": 500,
      "StoreFullContentInIndex": true,
      "UseSimpleQueryString": true,
      "DefaultAnalyzer": "standard"
    }
  }
}
```

## Tenancy strategy

- `Shared` (default): one index per `TKey`. Tenants are isolated by a mandatory
  `term tenant_id` filter applied to every read and write.
- `PerTenant`: one index per `(TKey, tenant)` pair. Stricter physical isolation at the cost
  of one extra index per tenant. The framework still applies the `tenant_id` filter as
  defence-in-depth for misrouted bulk imports.

## Query syntax

By default the backend uses `simple_query_string` with the restricted flag set
`AND | OR | PHRASE | PREFIX`. Lucene's full `query_string` (regex, fuzzy, field-targeted
operators) is reachable only when the request carries `UseAdvancedSyntax = true`, which the
consumer endpoint MUST gate on a `Search.Advanced.Execute` permission. Anonymous traffic
sees only the restricted analyzer — the historic Lucene injection surface stays closed.

## GDPR Art. 17 cascade

The package ships an `IIndexedDataEraser` that fans out a single `delete_by_query` across
every registered `TKey`. `Granit.Indexing.Privacy` picks it up automatically when its module
is also registered.

`delete_by_query` is a logical delete; physical disposal happens at the next segment
merge or via an explicit `forcemerge` schedule. The framework documents this as an
operational concern — Article 17 is satisfied because the data is no longer addressable,
but bit-level disposal depends on the host's storage policy.
