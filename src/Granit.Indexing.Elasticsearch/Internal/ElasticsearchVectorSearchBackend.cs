using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Granit.Indexing.Embeddings;
using Granit.MultiTenancy;

namespace Granit.Indexing.Elasticsearch.Internal;

/// <summary>
/// Elasticsearch-backed <see cref="IVectorSearchBackend{TKey, TResult}"/>. Runs a
/// <c>knn</c> query against the <c>dense_vector</c> field of
/// <see cref="IndexedEntryDocument"/>, restricted to the active tenant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant isolation.</b> Every search query includes a mandatory
/// <c>term tenant_id</c> filter resolved from <see cref="ICurrentTenant.Id"/>. Same
/// defence-in-depth as the lexical backend.
/// </para>
/// <para>
/// <b>Score sign convention.</b> Elasticsearch <c>knn</c> already returns higher-is-
/// better cosine-similarity scores (the framework convention). The backend forwards
/// them verbatim — no flip needed.
/// </para>
/// </remarks>
internal sealed class ElasticsearchVectorSearchBackend<TKey, TResult> : IVectorSearchBackend<TKey, TResult>
    where TKey : notnull
{
    internal const string BackendName = "elasticsearch_knn";

    private readonly ElasticsearchClient _client;
    private readonly IndexNameResolver _indexNames;
    private readonly IndexBootstrapper _bootstrapper;
    private readonly ICurrentTenant _currentTenant;
    private readonly Func<IndexedEntryDocument, TKey> _keyProjection;
    private readonly Func<IndexedEntryDocument, TResult> _resultProjection;

    public ElasticsearchVectorSearchBackend(
        ElasticsearchClient client,
        IndexNameResolver indexNames,
        IndexBootstrapper bootstrapper,
        ICurrentTenant currentTenant,
        Func<IndexedEntryDocument, TKey> keyProjection,
        Func<IndexedEntryDocument, TResult> resultProjection)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(indexNames);
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(keyProjection);
        ArgumentNullException.ThrowIfNull(resultProjection);
        _client = client;
        _indexNames = indexNames;
        _bootstrapper = bootstrapper;
        _currentTenant = currentTenant;
        _keyProjection = keyProjection;
        _resultProjection = resultProjection;
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

        Guid? tenantId = _currentTenant.Id;
        string indexName = _indexNames.Resolve(typeof(TKey), tenantId);
        await _bootstrapper.EnsureAsync(indexName, cancellationToken).ConfigureAwait(false);

        Query tenantFilter = new TermQuery
        {
            Field = "tenantId.keyword",
            Value = tenantId?.ToString() ?? string.Empty,
        };

        // ES's k MUST be >= numCandidates >= (offset + limit). Boost numCandidates to
        // 2x the requested window so the HNSW graph traversal explores enough
        // neighbourhood to give the slice good quality.
        int knnK = offset + limit + 1;
        int numCandidates = Math.Max(knnK * 2, 100);

        float[] queryVector = queryEmbedding.ToArray();

        SearchResponse<IndexedEntryDocument> response = await _client.SearchAsync<IndexedEntryDocument>(s => s
            .Indices(indexName)
            .From(offset)
            .Size(limit + 1)
            .Knn(k => k
                .Field(d => d.Embedding!)
                .QueryVector(queryVector)
                .K(knnK)
                .NumCandidates(numCandidates)
                .Filter(tenantFilter))
            .TrackTotalHits(new Elastic.Clients.Elasticsearch.Core.Search.TrackHits(false)), cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(
                $"Elasticsearch kNN search failed on '{indexName}': {response.DebugInformation}");
        }

        var rawHits = response.Hits.ToList();
        int returned = Math.Min(rawHits.Count, limit);
        bool hasMore = rawHits.Count > limit;

        var hits = new SearchHit<TKey, TResult>[returned];
        for (int i = 0; i < returned; i++)
        {
            IndexedEntryDocument doc = rawHits[i].Source!;
            hits[i] = new SearchHit<TKey, TResult>(
                _keyProjection(doc),
                _resultProjection(doc),
                rawHits[i].Score ?? 0d);
        }

        return new BackendSearchPage<TKey, TResult>(hits, hasMore);
    }
}
