using Testcontainers.Elasticsearch;
using Xunit;

namespace Granit.Indexing.Elasticsearch.Tests.Integration;

/// <summary>
/// Elasticsearch 9.x container shared across the integration suite. Boots once; tests
/// use unique index prefixes to avoid cross-pollution.
/// </summary>
public sealed class ElasticsearchFixture : IAsyncLifetime
{
    private readonly ElasticsearchContainer _container = new ElasticsearchBuilder("elasticsearch:9.4.2")
        // Disable disk-based shard allocation: in CI the runner disk may be >85% full,
        // triggering the high watermark and blocking even PRIMARY shard allocation →
        // 503 unavailable_shards_exception after a 1-minute timeout on every IndexAsync.
        .WithEnvironment("cluster.routing.allocation.disk.threshold_enabled", "false")
        .Build();

    public string Uri => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
