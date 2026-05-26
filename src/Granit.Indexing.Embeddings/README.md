# Granit.Indexing.Embeddings

Opt-in semantic + hybrid search add-on for `Granit.Indexing`. Decorates the host's
existing indexer with an embedding generator, decorates the lexical search backend with
a hybrid retriever that fuses BM25/tsvector results with cosine kNN via Reciprocal Rank
Fusion (Cormack et al. 2009).

## When to wire it in

The default `Granit.Indexing` pipeline is purely lexical — `tsvector` on Postgres,
BM25 on Elasticsearch. Lexical retrieval misses synonym matches: a query
*"comment résilier mon abonnement"* against a doc worded *"procédure de
désinscription"* returns nothing.

Reach for this package when:

- The corpus mixes languages or registers (formal/colloquial) and lexical recall
  alone leaves obvious matches on the table.
- You already pay for an embedding model (OpenAI `text-embedding-3-small`,
  `bge-large`, Ollama-hosted `nomic-embed`, …).
- The marginal cost of one embedding call per indexed entry and per search query is
  acceptable.

## Registration

```csharp
// 1. Host registers an IEmbeddingGenerator from any AI SDK (M.E.AI.Abstractions).
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(...);

// 2. Storage backend's regular Add… extension (EF or ES).
builder.Services.AddGranitIndexing();
builder.Services.AddGranitIndexingEntityFrameworkCore(opts => opts.UseNpgsql(cs), typeof(Guid));

// 3. Storage backend's VECTOR extension.
builder.Services.AddGranitIndexingEmbeddingsBackend<Guid, MyResult>(row => new MyResult(row.Key, row.Content));

// 4. This package — decorates indexer + search backend.
builder.Services.AddGranitIndexingEmbeddings();
builder.Services.AddGranitIndexingEmbeddingsWriter<Guid>();
builder.Services.AddGranitIndexingHybridSearch<Guid, MyResult>();
```

`AddGranitIndexingEmbeddingsWriter` and `AddGranitIndexingHybridSearch` MUST be the
LAST decorators applied to their target services so the embedding write reaches the
storage backend. They both fast-fail at composition time when their dependencies are
missing — no silent degraded mode.

## Configuration

```json
{
  "Indexing": {
    "Embeddings": {
      "Dimensions": 1536,
      "EmbeddingModelId": "text-embedding-3-small",
      "RrfK": 60,
      "RrfFetchPoolSize": 200,
      "RecommendHnswReindexCadenceDays": 7
    }
  }
}
```

`Dimensions` must match BOTH the model used by your `IEmbeddingGenerator` AND the
column type in your storage backend (`vector(N)` on pgvector, `dims: N` on
Elasticsearch). A mismatch fails at the first index attempt.

## Hybrid retrieval — how it works

Each `SearchAsync` call:

1. Embeds `request.Query` via `IEmbeddingGenerator` once.
2. Fetches a deep pool from both channels in parallel
   (`poolSize = max(RrfFetchPoolSize, (offset + limit) * 2)`).
3. Fuses via Reciprocal Rank Fusion:
   `score(d) = Σ 1 / (k + denseRank_i(d))`.
   Dense ranking means tied raw scores share a rank — the second equal-score doc is
   not unfairly penalised.
4. Slices `Skip(offset).Take(limit)` AFTER the fusion. Backend-level pagination kills
   RRF math, so the fuser MUST see the deep union before the page boundary.

When the embedding call fails (transport, timeout), the backend degrades gracefully to
lexical-only with the original `(offset, limit)`. The user still gets results.

## GDPR Art. 17 (VULN-201) compliance

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

## Cost ceiling — deferred

This package does NOT yet rate-limit outbound `IEmbeddingGenerator` calls. The
`IAICallRateLimiter` factored during I-F3.2 will be wired in here as a follow-up once
the cost-accounting contract is fleshed out. Hosts that need a hard cap today can
register a wrapper around their `IEmbeddingGenerator`.
