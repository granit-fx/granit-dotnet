using Granit.Indexing.Elasticsearch.Extensions;
using Granit.Indexing.Elasticsearch.Options;
using Granit.Indexing.Extensions;
using Granit.MultiTenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Elasticsearch.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private static readonly Dictionary<string, string?> BaseConfig = new()
    {
        ["Indexing:Elasticsearch:Uri"] = "http://localhost:9200",
    };

    [Fact]
    public void AddGranitIndexingElasticsearch_throws_when_no_key_types_supplied()
    {
        ServiceCollection services = [];

        Should.Throw<ArgumentException>(() =>
            services.AddGranitIndexingElasticsearch(configureClient: null));
    }

    [Fact]
    public void AddGranitIndexingElasticsearch_replaces_pre_registered_indexer_to_avoid_dual_backends()
    {
        // The host explicitly opted into Elasticsearch — the EF default must NOT also
        // fire on the same dispatch. The extension strips any pre-existing IIndexer<TKey>
        // before adding ES. Locking this prevents a regression where two backends would
        // double-write on every IndexAsync call. We inspect ServiceDescriptors directly
        // because activating the indexer pulls in ILocalEventBus / metrics / tenant — not
        // the contract this test cares about.
        ServiceCollection services = BuildBaseServices();
        services.AddSingleton<IIndexer<Guid>, FakeEfIndexer>();

        services.AddGranitIndexingElasticsearch(configureClient: null, typeof(Guid));

        ServiceDescriptor[] indexerDescriptors = [.. services.Where(d => d.ServiceType == typeof(IIndexer<Guid>))];
        indexerDescriptors.Length.ShouldBe(1);
        indexerDescriptors[0].ImplementationType.ShouldNotBe(typeof(FakeEfIndexer));
        indexerDescriptors[0].ImplementationType!.Name.ShouldStartWith("ElasticsearchIndexer");
    }

    [Fact]
    public void AddGranitIndexingElasticsearch_replaces_pre_registered_data_eraser_to_avoid_double_cascade()
    {
        // Same rationale on the GDPR side: Granit.Indexing.Privacy fans out across every
        // registered IIndexedDataEraser. Two erasers means two delete passes per request,
        // which is at best noise in metrics and at worst breaks idempotency assumptions
        // downstream.
        ServiceCollection services = BuildBaseServices();
        services.AddSingleton<IIndexedDataEraser, FakeEfEraser>();

        services.AddGranitIndexingElasticsearch(configureClient: null, typeof(Guid));

        ServiceDescriptor[] eraserDescriptors = [.. services.Where(d => d.ServiceType == typeof(IIndexedDataEraser))];
        eraserDescriptors.Length.ShouldBe(1);
        eraserDescriptors[0].ImplementationType.ShouldNotBe(typeof(FakeEfEraser));
    }

    [Fact]
    public void AddGranitIndexingElasticsearch_binds_options_from_configuration()
    {
        ServiceCollection services = BuildBaseServices(extra: new Dictionary<string, string?>
        {
            ["Indexing:Elasticsearch:Strategy"] = "PerTenant",
            ["Indexing:Elasticsearch:IndexPrefix"] = "acme",
            ["Indexing:Elasticsearch:StoreFullContentInIndex"] = "false",
        });

        services.AddGranitIndexingElasticsearch(configureClient: null, typeof(Guid));

        ServiceProvider sp = services.BuildServiceProvider();
        IndexingElasticsearchOptions options = sp.GetRequiredService<IndexingElasticsearchOptions>();

        options.Uri.ShouldBe("http://localhost:9200");
        options.Strategy.ShouldBe(ElasticsearchTenancyStrategy.PerTenant);
        options.IndexPrefix.ShouldBe("acme");
        options.StoreFullContentInIndex.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitIndexingElasticsearchBackend_replaces_pre_registered_search_backend()
    {
        ServiceCollection services = BuildBaseServices();
        services.AddGranitIndexingElasticsearch(configureClient: null, typeof(Guid));
        services.AddSingleton<ISearchBackend<Guid, FakeResponse>, FakeEfSearchBackend>();

        services.AddGranitIndexingElasticsearchBackend<Guid, FakeResponse>(
            keyProjection: d => Guid.Parse(d.Key),
            resultProjection: d => new FakeResponse(d.Key));

        ServiceDescriptor[] backendDescriptors = [.. services.Where(d => d.ServiceType == typeof(ISearchBackend<Guid, FakeResponse>))];
        backendDescriptors.Length.ShouldBe(1);
        backendDescriptors[0].ImplementationType.ShouldBeNull();
        backendDescriptors[0].ImplementationFactory.ShouldNotBeNull();
    }

    private static ServiceCollection BuildBaseServices(Dictionary<string, string?>? extra = null)
    {
        Dictionary<string, string?> config = new(BaseConfig);
        if (extra is not null)
        {
            foreach (KeyValuePair<string, string?> kv in extra)
            {
                config[kv.Key] = kv.Value;
            }
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        ServiceCollection services = [];
        services.AddSingleton(configuration);
        services.AddSingleton<IConfigurationRoot>(sp => (IConfigurationRoot)configuration);
        services.AddOptions();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton<ICurrentTenant>(NullTenantContext.Instance);
        services.AddGranitIndexing();
        return services;
    }

    private sealed class FakeEfIndexer : IIndexer<Guid>
    {
        public Task IndexAsync(IndexedEntry<Guid> entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(Guid key, Guid? tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeEfEraser : IIndexedDataEraser
    {
        public string Name => "fake_ef";
        public Task<int> EraseAsync(Guid? tenantId, Guid dataSubjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed record FakeResponse(string Key);

    private sealed class FakeEfSearchBackend : ISearchBackend<Guid, FakeResponse>
    {
        public string Name => "fake_ef_search";
        public Task<BackendSearchPage<Guid, FakeResponse>> SearchAsync(SearchRequest request, int offset, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendSearchPage<Guid, FakeResponse>([], false));
    }
}
