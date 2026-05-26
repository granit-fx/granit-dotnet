using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Granit.Indexing.Elasticsearch.Options;
using Granit.MultiTenancy;

namespace Granit.Indexing.Elasticsearch.Internal;

/// <summary>
/// Elasticsearch <see cref="ISearchBackend{TKey, TResult}"/>. Builds a BM25 multi-field
/// query over <c>content</c>, <c>summary</c>, and <c>tags</c>, restricted to the active
/// tenant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Query syntax.</b> Default uses <c>simple_query_string</c>
/// restricted to the <c>AND | OR | PHRASE | PREFIX</c> flag set. Full Lucene
/// <c>query_string</c> (regex, fuzzy, field-targeted operators) is intentionally not
/// reachable from this path; consumer endpoints expose it behind a
/// <c>Search.Advanced.Execute</c> permission and set
/// <see cref="SearchRequest.UseAdvancedSyntax"/> after the check — the backend then upgrades
/// to <c>query_string</c> with the same flag-restricted analyzer.
/// </para>
/// <para>
/// <b>Tenant isolation.</b> Every search query includes a mandatory
/// <c>term tenant_id</c> filter resolved from <see cref="ICurrentTenant.Id"/>. Even on the
/// <see cref="ElasticsearchTenancyStrategy.PerTenant"/> layout — where the index choice
/// already constrains the result set — the filter remains in place as defence-in-depth
/// for misrouted bulk-import scenarios.
/// </para>
/// </remarks>
internal sealed class ElasticsearchSearchBackend<TKey, TResult> : ISearchBackend<TKey, TResult>
{
    private readonly ElasticsearchClient _client;
    private readonly IndexingElasticsearchOptions _options;
    private readonly IndexNameResolver _indexNames;
    private readonly IndexBootstrapper _bootstrapper;
    private readonly ICurrentTenant _currentTenant;
    private readonly Func<IndexedEntryDocument, TKey> _keyProjection;
    private readonly Func<IndexedEntryDocument, TResult> _resultProjection;

    public ElasticsearchSearchBackend(
        ElasticsearchClient client,
        IndexingElasticsearchOptions options,
        IndexNameResolver indexNames,
        IndexBootstrapper bootstrapper,
        ICurrentTenant currentTenant,
        Func<IndexedEntryDocument, TKey> keyProjection,
        Func<IndexedEntryDocument, TResult> resultProjection)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(indexNames);
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(keyProjection);
        ArgumentNullException.ThrowIfNull(resultProjection);
        _client = client;
        _options = options;
        _indexNames = indexNames;
        _bootstrapper = bootstrapper;
        _currentTenant = currentTenant;
        _keyProjection = keyProjection;
        _resultProjection = resultProjection;
    }

    public string Name => ElasticsearchIndexer<TKey>.BackendName;

    public async Task<BackendSearchPage<TKey, TResult>> SearchAsync(
        SearchRequest request,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Guid? tenantId = _currentTenant.Id;
        string indexName = _indexNames.Resolve(typeof(TKey), tenantId);
        await _bootstrapper.EnsureAsync(indexName, cancellationToken).ConfigureAwait(false);

        Query textQuery = BuildTextQuery(request);
        Query tenantFilter = new TermQuery
        {
            Field = "tenantId",
            Value = tenantId?.ToString() ?? string.Empty,
        };
        Query composite = new BoolQuery
        {
            Must = [textQuery],
            Filter = [tenantFilter],
        };

        SearchResponse<IndexedEntryDocument> response = await _client
            .SearchAsync<IndexedEntryDocument>(s => s
                .Indices(indexName)
                .From(offset)
                .Size(limit + 1)
                .Query(composite)
                .TrackTotalHits(new Elastic.Clients.Elasticsearch.Core.Search.TrackHits(false)), cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(
                $"Elasticsearch search failed on '{indexName}': {response.DebugInformation}");
        }

        var rawHits = response.Hits.ToList();
        int returned = Math.Min(rawHits.Count, limit);

        var hits = new SearchHit<TKey, TResult>[returned];
        for (int i = 0; i < returned; i++)
        {
            IndexedEntryDocument doc = rawHits[i].Source!;
            hits[i] = new SearchHit<TKey, TResult>(
                _keyProjection(doc),
                _resultProjection(doc),
                rawHits[i].Score ?? 0d);
        }

        bool hasMore = rawHits.Count > limit;
        return new BackendSearchPage<TKey, TResult>(hits, hasMore);
    }

    private Query BuildTextQuery(SearchRequest request)
    {
        string query = request.Query ?? string.Empty;
        string analyzer = ResolveAnalyzer(request.Language);

        if (request.UseAdvancedSyntax)
        {
            // Endpoint already gated this on Search.Advanced.Execute; upgrade to the full
            // query_string syntax but keep the same multi-field projection.
            return new QueryStringQuery
            {
                Query = query,
                Analyzer = analyzer,
                Fields = Fields.FromStrings(["content", "summary", "tags"]),
                DefaultOperator = Operator.And,
            };
        }

        if (_options.UseSimpleQueryString)
        {
            return new SimpleQueryStringQuery
            {
                Query = query,
                Analyzer = analyzer,
                Fields = Fields.FromStrings(["content", "summary", "tags"]),
                Flags = SimpleQueryStringFlags.And | SimpleQueryStringFlags.Or
                    | SimpleQueryStringFlags.Phrase | SimpleQueryStringFlags.Prefix,
                DefaultOperator = Operator.And,
            };
        }

        return new MultiMatchQuery
        {
            Query = query,
            Analyzer = analyzer,
            Fields = Fields.FromStrings(["content", "summary", "tags"]),
            Type = TextQueryType.BestFields,
            Operator = Operator.And,
        };
    }

    private string ResolveAnalyzer(string? languageOverride)
    {
        string? language = languageOverride;
        if (!string.IsNullOrEmpty(language)
            && _options.LanguageAnalyzers.TryGetValue(language, out string? analyzer))
        {
            return analyzer;
        }
        return _options.DefaultAnalyzer;
    }
}
