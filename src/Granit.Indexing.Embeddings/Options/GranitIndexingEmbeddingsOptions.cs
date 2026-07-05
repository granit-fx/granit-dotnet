using System.ComponentModel.DataAnnotations;

namespace Granit.Indexing.Embeddings.Options;

/// <summary>
/// Configuration options for <c>Granit.Indexing.Embeddings</c>. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class GranitIndexingEmbeddingsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Indexing:Embeddings";

    /// <summary>
    /// <c>Granit.AI</c> workspace name resolved via
    /// <c>IAIEmbeddingGeneratorFactory.CreateAsync(workspaceName)</c> at
    /// indexing / search time. Required — the framework no longer accepts a flat
    /// <c>IEmbeddingGenerator</c> singleton; everything goes through the workspace
    /// catalogue so per-tenant provider routing, cost monitoring, and the
    /// ISO 27001 audit trail stay consistent across modules.
    /// </summary>
    /// <remarks>
    /// Leave at an empty string to opt into the <c>Granit.AI</c> default workspace
    /// (matches <c>IAIEmbeddingGeneratorFactory.CreateAsync(null)</c> semantics).
    /// Misconfigurations surface as <c>AIWorkspaceNotFoundException</c> on the first
    /// embedding call rather than at composition time — by design, since workspaces
    /// can come from a database loader that boots after DI.
    /// </remarks>
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>
    /// Dimensionality of the embedding vectors produced by the configured
    /// workspace's provider. Must match the model used (e.g. 1536 for OpenAI
    /// <c>text-embedding-3-small</c>, 768 for <c>bge-base</c>, 384 for
    /// <c>all-MiniLM-L6-v2</c>) AND the column type configured in the storage
    /// backend (<c>vector(N)</c> on pgvector, <c>dims: N</c> on Elasticsearch
    /// <c>dense_vector</c>).
    /// </summary>
    [Range(1, 8192)]
    public int Dimensions { get; set; }

    /// <summary>
    /// Free-form model identifier surfaced on metrics + logs (e.g.
    /// <c>"text-embedding-3-small"</c>). Informational only — the actual model
    /// is resolved via the workspace's provider configuration in <c>Granit.AI</c>.
    /// </summary>
    public string EmbeddingModelId { get; set; } = string.Empty;

    /// <summary>
    /// Smoothing constant of the Reciprocal Rank Fusion formula
    /// <c>score(d) = Σ 1 / (k + denseRank_i(d))</c>. Default <c>60</c> per Cormack
    /// et al. 2009; lower values amplify the contribution of high-ranking documents,
    /// higher values flatten the curve.
    /// </summary>
    [Range(1, 1000)]
    public int RrfK { get; set; } = 60;

    /// <summary>
    /// Minimum number of documents fetched from EACH backend (lexical + semantic)
    /// before the fuser runs. The hybrid backend expands this floor for deep
    /// pagination: actual pool size is <c>max(RrfFetchPoolSize, (offset + limit) * 2)</c>.
    /// Default <c>200</c> — large enough that page 1 results are stable across runs.
    /// </summary>
    /// <remarks>
    /// Backend-level pagination kills RRF math; the fuser MUST see a deep union
    /// before the page slice. Shrinking this floor below ~50 degrades quality on the
    /// long tail; raising it past ~500 spends round-trip latency for marginal gains.
    /// </remarks>
    [Range(10, 10_000)]
    public int RrfFetchPoolSize { get; set; } = 200;

    /// <summary>
    /// Informational hint emitted at startup to remind operators that HNSW indices
    /// retain stale vector pointers after <c>DELETE</c> until a <code>REINDEX
    /// CONCURRENTLY</code> (Postgres) or <c>forcemerge</c> (Elasticsearch) runs.
    /// Default <c>7</c> days — a reasonable cadence for typical GDPR Art. 17
    /// audit windows. The framework does NOT schedule the reindex itself.
    /// </summary>
    [Range(1, 365)]
    public int RecommendHnswReindexCadenceDays { get; set; } = 7;
}
