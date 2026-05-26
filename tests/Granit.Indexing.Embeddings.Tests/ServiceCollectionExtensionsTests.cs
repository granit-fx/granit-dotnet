using Granit.Indexing.Embeddings.Extensions;
using Granit.Indexing.Embeddings.Options;
using Granit.Indexing.Extensions;
using Granit.MultiTenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Embeddings.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitIndexingEmbeddings_validates_Dimensions_at_startup()
    {
        // Dimensions is required and must be in [1, 8192]. ValidateOnStart would surface
        // a misconfiguration loudly at app boot — here we trigger the validator manually
        // via Get() to keep the unit suite independent of a full IHostedService bootstrap.
        ServiceCollection services = BuildBaseServices(new Dictionary<string, string?>
        {
            ["Indexing:Embeddings:Dimensions"] = "0",
        });
        services.AddGranitIndexingEmbeddings();
        ServiceProvider sp = services.BuildServiceProvider();

        Should.Throw<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            _ = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GranitIndexingEmbeddingsOptions>>().Value);
    }

    [Fact]
    public void AddGranitIndexingEmbeddingsWriter_fast_fails_when_no_IEmbeddingGenerator_registered()
    {
        // The silent-degraded-mode trap: a host wires the decorator but forgets the
        // generator and discovers at indexing time that semantic search returns nothing.
        // Throwing at composition prevents that.
        ServiceCollection services = BuildBaseServices();
        services.AddSingleton<IIndexer<Guid>, FakeInnerIndexer>();

        Should.Throw<InvalidOperationException>(() =>
            services.AddGranitIndexingEmbeddingsWriter<Guid>());
    }

    [Fact]
    public void AddGranitIndexingEmbeddingsWriter_fast_fails_when_no_inner_IIndexer_registered()
    {
        // Decorator order matters: the storage backend MUST register IIndexer<TKey>
        // before this extension wraps it. A bare embeddings registration without a
        // backend is a broken host config; surface it now, not at first index.
        ServiceCollection services = BuildBaseServices();
        services.AddSingleton(Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>());

        Should.Throw<InvalidOperationException>(() =>
            services.AddGranitIndexingEmbeddingsWriter<Guid>());
    }

    [Fact]
    public void AddGranitIndexingHybridSearch_fast_fails_when_no_vector_backend_registered()
    {
        ServiceCollection services = BuildBaseServices();
        services.AddSingleton(Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>());
        services.AddSingleton<ISearchBackend<Guid, FakeResult>, FakeInnerSearchBackend>();

        Should.Throw<InvalidOperationException>(() =>
            services.AddGranitIndexingHybridSearch<Guid, FakeResult>());
    }

    [Fact]
    public void AddGranitIndexingEmbeddingsWriter_decorates_the_previously_registered_indexer()
    {
        // The decorator semantics: after the extension, exactly ONE IIndexer<TKey> is
        // registered, and resolving it returns the EmbeddingIndexer (which holds the
        // original FakeInnerIndexer as its inner field — we don't need to assert that
        // here, just that the decoration replaced the descriptor).
        ServiceCollection services = BuildBaseServices();
        services.AddSingleton(Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>());
        services.AddSingleton<IIndexer<Guid>, FakeInnerIndexer>();

        services.AddGranitIndexingEmbeddingsWriter<Guid>();

        ServiceDescriptor[] indexerDescriptors = [.. services.Where(d => d.ServiceType == typeof(IIndexer<Guid>))];
        indexerDescriptors.Length.ShouldBe(1);
        // The decorator descriptor uses an ImplementationFactory; the original
        // ImplementationType (FakeInnerIndexer) is no longer the surface registration.
        indexerDescriptors[0].ImplementationType.ShouldBeNull();
    }

    private static ServiceCollection BuildBaseServices(Dictionary<string, string?>? extra = null)
    {
        Dictionary<string, string?> config = new()
        {
            ["Indexing:Embeddings:Dimensions"] = "1536",
        };
        if (extra is not null)
        {
            foreach ((string k, string? v) in extra)
            {
                config[k] = v;
            }
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        ServiceCollection services = [];
        services.AddSingleton(configuration);
        services.AddOptions();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton<ICurrentTenant>(NullTenantContext.Instance);
        services.AddGranitIndexing();
        return services;
    }

    private sealed class FakeInnerIndexer : IIndexer<Guid>
    {
        public Task IndexAsync(IndexedEntry<Guid> entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(Guid key, Guid? tenantId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed record FakeResult(string Value);

    private sealed class FakeInnerSearchBackend : ISearchBackend<Guid, FakeResult>
    {
        public string Name => "fake";

        public Task<BackendSearchPage<Guid, FakeResult>> SearchAsync(SearchRequest request, int offset, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendSearchPage<Guid, FakeResult>([], false));
    }
}
