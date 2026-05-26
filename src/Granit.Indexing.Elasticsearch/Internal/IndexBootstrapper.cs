using System.Collections.Concurrent;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Granit.Indexing.Elasticsearch.Options;
using Granit.Indexing.Embeddings.Options;
using Microsoft.Extensions.Options;

namespace Granit.Indexing.Elasticsearch.Internal;

/// <summary>
/// Creates Elasticsearch indices on first use with the mapping required by
/// <see cref="IndexedEntryDocument"/>. Idempotent: subsequent calls for the same index
/// short-circuit on the in-memory cache, and a concurrent <c>resource_already_exists_exception</c>
/// from the cluster is swallowed.
/// </summary>
/// <remarks>
/// The bootstrapper deliberately does NOT use an index template — templates apply only to
/// indices created after the template lands, leaving any pre-existing index with a stale
/// mapping. Direct <c>CreateAsync</c> is explicit and tests cleanly against Testcontainers.
/// </remarks>
internal sealed class IndexBootstrapper
{
    private readonly ElasticsearchClient _client;
    private readonly IndexingElasticsearchOptions _options;
    private readonly int? _embeddingDimensions;
    private readonly ConcurrentDictionary<string, byte> _ensuredIndices = new(StringComparer.Ordinal);

    public IndexBootstrapper(
        ElasticsearchClient client,
        IndexingElasticsearchOptions options,
        IOptions<GranitIndexingEmbeddingsOptions>? embeddingsOptions = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options;
        // Embeddings are opt-in. The IOptions<> can be null when the host hasn't called
        // AddGranitIndexingEmbeddings(). When present but with Dimensions = 0 (default
        // sentinel) the field is also dropped.
        _embeddingDimensions = embeddingsOptions?.Value.Dimensions is int d and > 0 ? d : null;
    }

    public async Task EnsureAsync(string indexName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        if (_ensuredIndices.ContainsKey(indexName))
        {
            return;
        }

        Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse existsResponse = await _client.Indices
            .ExistsAsync(indexName, cancellationToken)
            .ConfigureAwait(false);

        if (existsResponse.Exists)
        {
            _ensuredIndices.TryAdd(indexName, 0);
            return;
        }

        CreateIndexResponse createResponse = await _client.Indices
            .CreateAsync<IndexedEntryDocument>(indexName, c => c.Mappings(BuildMappings), cancellationToken)
            .ConfigureAwait(false);

        if (!createResponse.IsValidResponse
            && createResponse.ElasticsearchServerError?.Error?.Type != "resource_already_exists_exception")
        {
            throw new InvalidOperationException(
                $"Failed to create Elasticsearch index '{indexName}': {createResponse.DebugInformation}");
        }

        _ensuredIndices.TryAdd(indexName, 0);
    }

    private void BuildMappings(TypeMappingDescriptor<IndexedEntryDocument> mapping)
    {
        mapping.Properties(p =>
        {
            p
                .Keyword(d => d.Key, k => k.IgnoreAbove(512))
                .Keyword(d => d.TenantId)
                .Keyword(d => d.Language, k => k.IgnoreAbove(16))
                .Keyword(d => d.Tags, k => k.IgnoreAbove(256))
                .Keyword(d => d.DataSubjectId)
                .Boolean(d => d.IsTruncated)
                .IntegerNumber(d => d.CharCount)
                .Text(d => d.Content, t => ConfigureTextField(t, store: _options.StoreFullContentInIndex))
                .Text(d => d.Summary, t => ConfigureTextField(t, store: true));

            if (_embeddingDimensions is { } dims)
            {
                p.DenseVector(d => d.Embedding!, v => v
                    .Dims(dims)
                    .Index(true)
                    .Similarity(DenseVectorSimilarity.Cosine));
            }
        });
    }

    private void ConfigureTextField(TextPropertyDescriptor<IndexedEntryDocument> text, bool store)
    {
        text.Analyzer(_options.DefaultAnalyzer);
        text.Store(store);
    }
}
