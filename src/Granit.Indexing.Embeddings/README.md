# Granit.Indexing.Embeddings

Opt-in semantic + hybrid search add-on for `Granit.Indexing`. Decorates the host's
existing indexer with an embedding generator, decorates the lexical search backend with
a hybrid retriever that fuses BM25/tsvector results with cosine kNN via Reciprocal Rank
Fusion (Cormack et al. 2009).

Embedding generators are resolved through `Granit.AI`'s workspace-aware
`IAIEmbeddingGeneratorFactory` — same pattern as `Granit.LanguageDetection.AI`,
`Granit.Indexing.AI` (auto-tagger), `Granit.Notifications.AI`. Per-tenant provider
routing, cost monitoring, and the ISO 27001 audit trail stay consistent across modules.

## When to wire it in

The default `Granit.Indexing` pipeline is purely lexical — `tsvector` on Postgres,
BM25 on Elasticsearch. Lexical retrieval misses synonym matches: a query
*"comment résilier mon abonnement"* against a doc worded *"procédure de
désinscription"* returns nothing.

Reach for this package when:

- The corpus mixes languages or registers (formal/colloquial) and lexical recall
  alone leaves obvious matches on the table.
- You already operate an embedding model in a `Granit.AI` workspace (OpenAI
  `text-embedding-3-small`, `bge-large`, Ollama-hosted `nomic-embed`, …).
- The marginal cost of one embedding call per indexed entry and per search query is
  acceptable.

## Registration

```csharp
// 1. Wire Granit.AI (workspace catalogue + provider factories).
builder.Services.AddGranitAI();
// ...register your embedding-capable workspace (Ollama, OpenAI, Azure OpenAI, ...).

// 2. Storage backend's regular Add… extension (EF or ES).
builder.Services.AddGranitIndexing();
builder.Services.AddGranitIndexingEntityFrameworkCore(opts => opts.UseNpgsql(cs), typeof(Guid));

// 3. Storage backend's VECTOR extension.
builder.Services.AddGranitIndexingEmbeddingsBackend<Guid, MyResult>(
    row => new MyResult(row.Key, row.Content));

// 4. This package — decorates indexer + search backend.
builder.Services.AddGranitIndexingEmbeddings();
builder.Services.AddGranitIndexingEmbeddingsWriter<Guid>();
builder.Services.AddGranitIndexingHybridSearch<Guid, MyResult>();
```

`AddGranitIndexingEmbeddingsWriter` and `AddGranitIndexingHybridSearch` MUST be the
LAST decorators applied to their target services so the embedding write reaches the
storage backend. They both fast-fail at composition time when `Granit.AI` isn't wired
or the inner indexer / vector backend is missing — no silent degraded mode.

## Configuration

```json
{
  "Indexing": {
    "Embeddings": {
      "WorkspaceName": "default",
      "Dimensions": 1536,
      "EmbeddingModelId": "text-embedding-3-small",
      "RrfK": 60,
      "RrfFetchPoolSize": 200,
      "RecommendHnswReindexCadenceDays": 7
    }
  }
}
```

`WorkspaceName` is resolved through `IAIEmbeddingGeneratorFactory.CreateAsync(...)` —
leave at `""` to use the `Granit.AI` default workspace. Workspace mis-configuration
surfaces as `AIWorkspaceNotFoundException` on the first embedding call (not at
composition time), so a database-backed workspace catalogue that boots after DI is
supported.

`Dimensions` must match BOTH the model the workspace's provider returns AND the column
type in your storage backend (`vector(N)` on pgvector, `dims: N` on Elasticsearch). A
mismatch fails at the first index attempt.

## Hybrid retrieval — how it works

Each `SearchAsync` call:

1. Resolves an `IEmbeddingGenerator` for `WorkspaceName` via the factory, embeds
   `request.Query` once, disposes the generator.
2. Fetches a deep pool from both channels in parallel
   (`poolSize = max(RrfFetchPoolSize, (offset + limit) * 2)`).
3. Fuses via Reciprocal Rank Fusion:
   `score(d) = Σ 1 / (k + denseRank_i(d))`.
   Dense ranking means tied raw scores share a rank — the second equal-score doc is
   not unfairly penalised.
4. Slices `Skip(offset).Take(limit)` AFTER the fusion. Backend-level pagination kills
   RRF math, so the fuser MUST see the deep union before the page boundary.

When the embedding call fails (workspace mis-config, transport, timeout), the backend
degrades gracefully to lexical-only with the original `(offset, limit)`. The user still
gets results, the `granit.indexing.embeddings.vector_backend_misses` counter ticks so
operators can alert on outages.

## GDPR Art. 17 compliance

The CJEU ruled in 2024 that embeddings are personal data. The framework persists the
embedding on the **same row** as `Content` in every storage backend — when the
existing `IIndexedDataEraser` cascade fires, both atoms vanish atomically in a single
`DELETE`. No sidecar table, no orphan vectors.

### HNSW ghost vectors

On Postgres + pgvector, an HNSW index keeps a pointer to the deleted vector in its
graph until a `REINDEX INDEX CONCURRENTLY` runs. Similarly, Elasticsearch retains
deleted vectors until `forcemerge`. The framework cannot schedule this — it's an ops
concern. Recommended cadence: weekly (`RecommendHnswReindexCadenceDays = 7`).

```sql
-- Postgres example (pg_cron weekly)
SELECT cron.schedule('hnsw-purge', '0 3 * * 0',
    $$REINDEX INDEX CONCURRENTLY ix_indexed_entry_embedding$$);
```

## Migration from previous releases

Prior releases registered an `IEmbeddingGenerator<string, Embedding<float>>` directly
in DI. That path is gone — every embedding call now flows through `Granit.AI`'s
workspace factory.

Before:

```csharp
services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
    new OllamaEmbeddingGenerator("http://localhost:11434", "nomic-embed-text"));
services.AddGranitIndexingEmbeddings(o => o.Dimensions = 768);
services.AddGranitIndexingEmbeddingsWriter<Guid>();
```

After:

```csharp
services.AddGranitAI();
// ... wire your workspace catalogue with an Ollama provider on workspace "default" ...
services.AddGranitIndexingEmbeddings(o =>
{
    o.WorkspaceName = "default";   // or "" for the Granit.AI default
    o.Dimensions = 768;
});
services.AddGranitIndexingEmbeddingsWriter<Guid>();
```

## Cost ceiling — deferred

This package does NOT yet rate-limit outbound embedding calls. The `IAICallRateLimiter`
factored during I-F3.2 will be wired in as a follow-up once the cost-accounting
contract is fleshed out. Hosts that need a hard cap today can wrap their `Granit.AI`
provider registration with their own throttle.
