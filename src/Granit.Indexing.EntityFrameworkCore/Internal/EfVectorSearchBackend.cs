using Granit.Indexing.Embeddings;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore.Internal;

/// <summary>
/// EF-backed <see cref="IVectorSearchBackend{TKey, TResult}"/>. Translates the
/// caller-supplied query embedding into a Postgres cosine-distance kNN search against
/// <see cref="IndexedEntryRow{TKey}.Embedding"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cosine distance.</b> The backend orders by <c>r.Embedding &lt;=&gt; query</c>
/// (pgvector <c>vector_cosine_ops</c>); smaller distance = higher similarity. The
/// HNSW index built by <c>HasEmbeddingColumn</c> drives the query plan.
/// </para>
/// <para>
/// <b>Tenant isolation.</b> Inherited from <see cref="IndexingDbContext"/>: the
/// parameterised query filter on <see cref="IndexedEntryRow{TKey}.TenantId"/> is
/// rewritten into the SQL on every command, so cross-tenant vector matches are
/// impossible from the read path.
/// </para>
/// <para>
/// <b>Score sign convention.</b> The framework convention is "higher
/// <see cref="SearchHit{TKey, TResult}.Score"/> = more relevant". Pgvector returns
/// distance (lower is better), so the backend flips the sign: <c>Score = 1 - distance</c>
/// (cosine similarity in <c>[-1, 1]</c>). Dense ranking in the RRF fuser is robust to
/// either convention but consumers eyeballing raw scores benefit from the flip.
/// </para>
/// </remarks>
internal sealed class EfVectorSearchBackend<TKey, TResult> : IVectorSearchBackend<TKey, TResult>
    where TKey : notnull
{
    internal const string BackendName = "ef_pgvector";

    private readonly IDbContextFactory<IndexingDbContext> _factory;
    private readonly Func<IndexedEntryRow<TKey>, TResult> _projection;

    public EfVectorSearchBackend(
        IDbContextFactory<IndexingDbContext> factory,
        Func<IndexedEntryRow<TKey>, TResult> projection)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(projection);
        _factory = factory;
        _projection = projection;
    }

    public string Name => BackendName;

    public async Task<BackendSearchPage<TKey, TResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        SearchRequest request,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Vector query = new(queryEmbedding);

        await using IndexingDbContext db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<IndexedEntryRow<TKey>> q = db.Set<IndexedEntryRow<TKey>>()
            .Where(r => r.Embedding != null)
            .OrderBy(r => r.Embedding!.CosineDistance(query))
            .ThenBy(r => r.Key);

        var rows = await q
            .Skip(offset)
            .Take(limit + 1)
            .Select(r => new
            {
                Row = r,
                Distance = r.Embedding!.CosineDistance(query),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMore = rows.Count > limit;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var hits = new SearchHit<TKey, TResult>[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            // Cosine similarity in [-1, 1]; flip the distance so "higher is better"
            // matches the framework's SearchHit.Score convention.
            double similarity = 1.0 - rows[i].Distance;
            hits[i] = new SearchHit<TKey, TResult>(
                rows[i].Row.Key,
                _projection(rows[i].Row),
                similarity);
        }

        return new BackendSearchPage<TKey, TResult>(hits, hasMore);
    }
}
