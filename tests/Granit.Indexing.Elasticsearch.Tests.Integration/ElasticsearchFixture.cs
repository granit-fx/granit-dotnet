using Testcontainers.Elasticsearch;
using Xunit;

namespace Granit.Indexing.Elasticsearch.Tests.Integration;

/// <summary>
/// Elasticsearch 8.x container shared across the integration suite. Boots once; tests
/// use unique index prefixes to avoid cross-pollution.
/// </summary>
public sealed class ElasticsearchFixture : IAsyncLifetime
{
    private readonly ElasticsearchContainer _container = new ElasticsearchBuilder("elasticsearch:8.15.3")
        .Build();

    public string Uri => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
