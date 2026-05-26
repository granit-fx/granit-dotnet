using Granit.Indexing.Elasticsearch.Extensions;
using Granit.Indexing.Extensions;
using Granit.MultiTenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Elasticsearch.Tests.Integration;

/// <summary>
/// End-to-end round-trip against a real Elasticsearch container: index → search → erase.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ElasticsearchBackendTests : IClassFixture<ElasticsearchFixture>
{
    private readonly ElasticsearchFixture _fixture;
    private static readonly Guid TenantA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public ElasticsearchBackendTests(ElasticsearchFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Index_and_search_round_trip_returns_the_indexed_entry()
    {
        ServiceProvider sp = BuildHost(TenantA, prefix: $"itest-roundtrip-{Guid.NewGuid():N}");
        IIndexer<Guid> indexer = sp.GetRequiredService<IIndexer<Guid>>();
        ISearchBackend<Guid, EntrySnapshot> backend = sp.GetRequiredService<ISearchBackend<Guid, EntrySnapshot>>();

        var key = Guid.NewGuid();
        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = key,
            TenantId = TenantA,
            Content = "the quick brown fox jumps over the lazy dog",
            Language = "en",
            CharCount = 43,
        }, TestContext.Current.CancellationToken);

        // Wait for the refresh interval (Testcontainers defaults to 1s).
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);

        BackendSearchPage<Guid, EntrySnapshot> page = await backend.SearchAsync(
            new SearchRequest("quick fox"), offset: 0, limit: 10, TestContext.Current.CancellationToken);

        page.Hits.Count.ShouldBe(1);
        page.Hits[0].Key.ShouldBe(key);
    }

    [Fact]
    public async Task Search_does_not_leak_across_tenants()
    {
        // Tenant isolation is a write-time AND read-time invariant: even though tenant A
        // indexed a matching document, tenant B's search MUST see nothing. The backend
        // injects a mandatory term tenant_id filter on every query — losing it would be
        // an ISO 27001 A.9.4 failure.
        string prefix = $"itest-isolation-{Guid.NewGuid():N}";

        // Tenant A indexes.
        ServiceProvider hostA = BuildHost(TenantA, prefix);
        IIndexer<Guid> indexerA = hostA.GetRequiredService<IIndexer<Guid>>();
        await indexerA.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = TenantA,
            Content = "internal report — confidential",
            Language = "en",
            CharCount = 31,
        }, TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);

        // Tenant B searches the same index family.
        ServiceProvider hostB = BuildHost(TenantB, prefix);
        ISearchBackend<Guid, EntrySnapshot> backendB = hostB.GetRequiredService<ISearchBackend<Guid, EntrySnapshot>>();

        BackendSearchPage<Guid, EntrySnapshot> page = await backendB.SearchAsync(
            new SearchRequest("confidential"), offset: 0, limit: 10, TestContext.Current.CancellationToken);

        page.Hits.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GDPR_eraser_deletes_only_the_target_subject_within_the_tenant()
    {
        string prefix = $"itest-gdpr-{Guid.NewGuid():N}";
        ServiceProvider sp = BuildHost(TenantA, prefix);
        IIndexer<Guid> indexer = sp.GetRequiredService<IIndexer<Guid>>();
        IIndexedDataEraser eraser = sp.GetRequiredService<IIndexedDataEraser>();

        var subject = Guid.NewGuid();
        var otherSubject = Guid.NewGuid();

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = TenantA,
            Content = "personal data about subject one",
            DataSubjectId = subject,
            CharCount = 31,
        }, TestContext.Current.CancellationToken);

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = TenantA,
            Content = "personal data about subject two",
            DataSubjectId = otherSubject,
            CharCount = 31,
        }, TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);

        int deleted = await eraser.EraseAsync(TenantA, subject, TestContext.Current.CancellationToken);
        deleted.ShouldBe(1);

        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);

        ISearchBackend<Guid, EntrySnapshot> backend = sp.GetRequiredService<ISearchBackend<Guid, EntrySnapshot>>();
        BackendSearchPage<Guid, EntrySnapshot> remaining = await backend.SearchAsync(
            new SearchRequest("personal data"), offset: 0, limit: 10, TestContext.Current.CancellationToken);

        remaining.Hits.Count.ShouldBe(1);
    }

    private ServiceProvider BuildHost(Guid tenantId, string prefix)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Indexing:Elasticsearch:Uri"] = _fixture.Uri,
                ["Indexing:Elasticsearch:IndexPrefix"] = prefix,
            })
            .Build();

        ServiceCollection services = [];
        services.AddSingleton(configuration);
        services.AddOptions();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton<ICurrentTenant>(new FixedTenant(tenantId));
        services.AddGranitIndexing();
        services.AddGranitIndexingElasticsearch(
            configureClient: s => s.ServerCertificateValidationCallback((_, _, _, _) => true),
            typeof(Guid));
        services.AddGranitIndexingElasticsearchBackend<Guid, EntrySnapshot>(
            keyProjection: d => Guid.Parse(d.Key),
            resultProjection: d => new EntrySnapshot(d.Key, d.Content));

        return services.BuildServiceProvider();
    }

    public sealed record EntrySnapshot(string Key, string? Content);

    private sealed class FixedTenant(Guid? id) : ICurrentTenant
    {
        public bool IsAvailable => Id.HasValue;
        public Guid? Id { get; } = id;
        public string? Name => null;
        public IDisposable Change(Guid? id, string? name = null) => throw new NotSupportedException();
    }
}
