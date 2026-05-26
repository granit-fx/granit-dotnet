using Elastic.Clients.Elasticsearch;
using Granit.Events;
using Granit.Indexing.Diagnostics;
using Granit.Indexing.Elasticsearch.Options;
using Granit.Indexing.Events;
using Granit.MultiTenancy;

namespace Granit.Indexing.Elasticsearch.Internal;

/// <summary>
/// Elasticsearch-backed <see cref="IIndexer{TKey}"/>. Upserts an entry keyed by
/// <c>(tenantId, key)</c> into the resolved index.
/// </summary>
/// <remarks>
/// <para>
/// Document IDs follow <c>{tenant}:{key}</c> for the <see cref="ElasticsearchTenancyStrategy.Shared"/>
/// strategy and <c>{key}</c> for <see cref="ElasticsearchTenancyStrategy.PerTenant"/> — both
/// guarantee idempotent upserts inside the resolved index. Cross-tenant document ID
/// collisions are impossible in either layout.
/// </para>
/// <para>
/// On every successful write, the indexer publishes <see cref="EntryIndexedEvent{TKey}"/>
/// to the local bus so consumer projections (downstream cache invalidation, metric
/// aggregators, …) stay in sync without coupling to the backend.
/// </para>
/// </remarks>
internal sealed class ElasticsearchIndexer<TKey> : IIndexer<TKey>
{
    internal const string BackendName = "elasticsearch";

    private readonly ElasticsearchClient _client;
    private readonly IndexingElasticsearchOptions _options;
    private readonly IndexNameResolver _indexNames;
    private readonly IndexBootstrapper _bootstrapper;
    private readonly ICurrentTenant _currentTenant;
    private readonly IndexingMetrics _metrics;
    private readonly ILocalEventBus _eventBus;

    public ElasticsearchIndexer(
        ElasticsearchClient client,
        IndexingElasticsearchOptions options,
        IndexNameResolver indexNames,
        IndexBootstrapper bootstrapper,
        ICurrentTenant currentTenant,
        IndexingMetrics metrics,
        ILocalEventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(indexNames);
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(eventBus);
        _client = client;
        _options = options;
        _indexNames = indexNames;
        _bootstrapper = bootstrapper;
        _currentTenant = currentTenant;
        _metrics = metrics;
        _eventBus = eventBus;
    }

    public async Task IndexAsync(IndexedEntry<TKey> entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Guid? tenantId = entry.TenantId ?? _currentTenant.Id;
        string indexName = _indexNames.Resolve(typeof(TKey), tenantId);
        string keyString = entry.Key?.ToString() ?? throw new InvalidOperationException(
            $"Indexed key for {typeof(TKey).Name} serialized to null. ES document IDs require a stable string projection.");
        string documentId = BuildDocumentId(tenantId, keyString);

        await _bootstrapper.EnsureAsync(indexName, cancellationToken).ConfigureAwait(false);

        IndexedEntryDocument document = new()
        {
            Key = keyString,
            TenantId = tenantId,
            Content = _options.StoreFullContentInIndex ? entry.Content : null,
            Language = entry.Language,
            Summary = entry.Summary,
            Tags = entry.Tags?.ToArray(),
            IsTruncated = entry.IsTruncated,
            CharCount = entry.CharCount,
            DataSubjectId = entry.DataSubjectId,
        };

        IndexResponse response = await _client
            .IndexAsync(document, idx => idx.Index(indexName).Id(documentId), cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsValidResponse)
        {
            _metrics.RecordEntryFailed(tenantId?.ToString(), BackendName, "es_index_failed");
            await _eventBus.PublishAsync(
                new EntryIndexingFailedEvent<TKey>(entry.Key, tenantId, BackendName, "es_index_failed"),
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Failed to index entry {documentId} into '{indexName}': {response.DebugInformation}");
        }

        _metrics.RecordEntryIndexed(tenantId?.ToString(), BackendName);
        await _eventBus.PublishAsync(
            new EntryIndexedEvent<TKey>(entry.Key, tenantId, BackendName),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(TKey key, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        string indexName = _indexNames.Resolve(typeof(TKey), tenantId);
        string keyString = key.ToString() ?? throw new InvalidOperationException(
            $"Indexed key for {typeof(TKey).Name} serialized to null.");
        string documentId = BuildDocumentId(tenantId, keyString);

        DeleteResponse response = await _client
            .DeleteAsync(indexName, documentId, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsValidResponse && response.Result != Result.NotFound)
        {
            throw new InvalidOperationException(
                $"Failed to remove entry {documentId} from '{indexName}': {response.DebugInformation}");
        }
    }

    private string BuildDocumentId(Guid? tenantId, string key) =>
        _options.Strategy == ElasticsearchTenancyStrategy.PerTenant
            ? key
            : $"{tenantId?.ToString("N") ?? "global"}:{key}";
}
