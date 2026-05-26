namespace Granit.Indexing.Embeddings;

/// <summary>
/// Backend-agnostic vector search port. Concrete implementations ship in the storage
/// backend packages (<c>Granit.Indexing.EntityFrameworkCore</c> for pgvector,
/// <c>Granit.Indexing.Elasticsearch</c> for <c>dense_vector</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant isolation.</b> Same contract as <see cref="ISearchBackend{TKey, TResult}"/>:
/// implementations MUST scope every kNN query to the active
/// <see cref="MultiTenancy.ICurrentTenant"/>. Cross-tenant leakage at the vector
/// layer would defeat the framework's ISO 27001 A.9.4 posture.
/// </para>
/// <para>
/// <b>Pool semantics.</b> When wired into <c>HybridSearchBackend</c>, the caller always
/// passes <c>offset = 0</c> and <c>limit = poolSize</c> so the Reciprocal Rank Fusion
/// step sees a deep union before pagination. Direct consumers (a future pure-semantic
/// search service) MAY use the <c>offset</c> + <c>limit</c> arguments for shallow probes.
/// </para>
/// </remarks>
public interface IVectorSearchBackend<TKey, TResult>
{
    /// <summary>Stable backend identifier emitted on metric tags (e.g. <c>"ef_pgvector"</c>, <c>"elasticsearch_knn"</c>).</summary>
    string Name { get; }

    /// <summary>
    /// Executes an exact-or-approximate kNN search against the configured vector
    /// store. Results MUST be ordered by descending similarity (cosine, by convention).
    /// </summary>
    /// <param name="queryEmbedding">The query vector — typically the embedding of the user's
    /// natural-language query produced once by the hybrid orchestrator.</param>
    /// <param name="request">User-supplied query parameters (tenant, language hint, page size).</param>
    /// <param name="offset">Zero-based row offset within the kNN result set.</param>
    /// <param name="limit">Maximum rows to return for this iteration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<BackendSearchPage<TKey, TResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        SearchRequest request,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);
}
