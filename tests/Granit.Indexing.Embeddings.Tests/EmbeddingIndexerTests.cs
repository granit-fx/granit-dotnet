using Granit.AI;
using Granit.Indexing.Embeddings.Diagnostics;
using Granit.Indexing.Embeddings.Internal;
using Granit.Indexing.Embeddings.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Embeddings.Tests;

public sealed class EmbeddingIndexerTests
{
    private static readonly float[] Vector = [0.1f, 0.2f, 0.3f];

    [Fact]
    public async Task IndexAsync_generates_embedding_enriches_entry_and_disposes_generator()
    {
        Harness h = new();
        var generator = FakeGenerator.Returning(Vector);
        h.Factory.Generator = generator;
        using MetricCollector<long> generated = new(h.MeterFactory.Meter, "granit.indexing.embeddings.generated");

        await h.Build().IndexAsync(Entry("hello world"), TestContext.Current.CancellationToken);

        IndexedEntry<int> written = h.Inner.Indexed.ShouldHaveSingleItem();
        written.Embedding.ShouldNotBeNull();
        written.Embedding!.Value.ToArray().ShouldBe(Vector);
        h.Factory.CreateCount.ShouldBe(1);
        generator.Disposed.ShouldBeTrue();
        generated.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
    }

    [Fact]
    public async Task IndexAsync_passes_null_workspace_when_option_is_empty()
    {
        Harness h = new();
        h.Options.WorkspaceName = string.Empty;
        h.Factory.Generator = FakeGenerator.Returning(Vector);

        await h.Build().IndexAsync(Entry("text"), TestContext.Current.CancellationToken);

        h.Factory.LastWorkspace.ShouldBeNull();
    }

    [Fact]
    public async Task IndexAsync_forwards_the_configured_workspace_name()
    {
        Harness h = new();
        h.Options.WorkspaceName = "documents";
        h.Factory.Generator = FakeGenerator.Returning(Vector);

        await h.Build().IndexAsync(Entry("text"), TestContext.Current.CancellationToken);

        h.Factory.LastWorkspace.ShouldBe("documents");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IndexAsync_with_blank_content_skips_generation_and_passes_entry_through(string content)
    {
        Harness h = new();
        IndexedEntry<int> entry = Entry(content);

        await h.Build().IndexAsync(entry, TestContext.Current.CancellationToken);

        h.Factory.CreateCount.ShouldBe(0);
        IndexedEntry<int> written = h.Inner.Indexed.ShouldHaveSingleItem();
        written.ShouldBeSameAs(entry);
        written.Embedding.ShouldBeNull();
    }

    [Fact]
    public async Task IndexAsync_persists_without_embedding_and_records_failure_on_empty_result()
    {
        Harness h = new();
        h.Factory.Generator = FakeGenerator.Empty();
        using MetricCollector<long> failed = new(h.MeterFactory.Meter, "granit.indexing.embeddings.failed");

        await h.Build().IndexAsync(Entry("text"), TestContext.Current.CancellationToken);

        h.Inner.Indexed.ShouldHaveSingleItem().Embedding.ShouldBeNull();
        CollectedMeasurement<long> snapshot = failed.GetMeasurementSnapshot().ShouldHaveSingleItem();
        snapshot.Value.ShouldBe(1);
        snapshot.Tags["reason"].ShouldBe("empty_result");
    }

    [Fact]
    public async Task IndexAsync_persists_without_embedding_and_records_failure_when_generator_throws()
    {
        Harness h = new();
        h.Factory.Generator = FakeGenerator.Throwing(new InvalidOperationException("boom"));
        using MetricCollector<long> failed = new(h.MeterFactory.Meter, "granit.indexing.embeddings.failed");

        await h.Build().IndexAsync(Entry("text"), TestContext.Current.CancellationToken);

        h.Inner.Indexed.ShouldHaveSingleItem().Embedding.ShouldBeNull();
        CollectedMeasurement<long> failure = failed.GetMeasurementSnapshot().ShouldHaveSingleItem();
        failure.Value.ShouldBe(1);
        failure.Tags["reason"].ShouldBe("transport");
    }

    [Fact]
    public async Task IndexAsync_does_not_swallow_cancellation()
    {
        Harness h = new();
        h.Factory.Generator = FakeGenerator.Throwing(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => h.Build().IndexAsync(Entry("text"), TestContext.Current.CancellationToken));

        h.Inner.Indexed.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_delegates_to_the_inner_indexer()
    {
        Harness h = new();
        var tenant = Guid.NewGuid();

        await h.Build().RemoveAsync(42, tenant, TestContext.Current.CancellationToken);

        h.Inner.Removed.ShouldHaveSingleItem().ShouldBe((42, (Guid?)tenant));
    }

    [Fact]
    public void Constructor_rejects_null_arguments()
    {
        Harness h = new();
        IOptions<GranitIndexingEmbeddingsOptions> opts = h.OptionsAccessor;

        Should.Throw<ArgumentNullException>(() => new EmbeddingIndexer<int>(null!, h.Factory, opts, h.Metrics, h.Tenant, NullLogger<EmbeddingIndexer<int>>.Instance));
        Should.Throw<ArgumentNullException>(() => new EmbeddingIndexer<int>(h.Inner, null!, opts, h.Metrics, h.Tenant, NullLogger<EmbeddingIndexer<int>>.Instance));
        Should.Throw<ArgumentNullException>(() => new EmbeddingIndexer<int>(h.Inner, h.Factory, null!, h.Metrics, h.Tenant, NullLogger<EmbeddingIndexer<int>>.Instance));
        Should.Throw<ArgumentNullException>(() => new EmbeddingIndexer<int>(h.Inner, h.Factory, opts, null!, h.Tenant, NullLogger<EmbeddingIndexer<int>>.Instance));
        Should.Throw<ArgumentNullException>(() => new EmbeddingIndexer<int>(h.Inner, h.Factory, opts, h.Metrics, null!, NullLogger<EmbeddingIndexer<int>>.Instance));
        Should.Throw<ArgumentNullException>(() => new EmbeddingIndexer<int>(h.Inner, h.Factory, opts, h.Metrics, h.Tenant, null!));
    }

    [Fact]
    public async Task IndexAsync_rejects_a_null_entry()
    {
        Harness h = new();
        await Should.ThrowAsync<ArgumentNullException>(
            () => h.Build().IndexAsync(null!, TestContext.Current.CancellationToken));
    }

    private static IndexedEntry<int> Entry(string content) =>
        new() { Key = 1, TenantId = null, Content = content };

    private sealed class Harness
    {
        public TestMeterFactory MeterFactory { get; } = new();
        public RecordingIndexer Inner { get; } = new();
        public FakeGeneratorFactory Factory { get; } = new();
        public GranitIndexingEmbeddingsOptions Options { get; } = new() { Dimensions = 3, EmbeddingModelId = "test-model" };
        public ICurrentTenant Tenant { get; } = Substitute.For<ICurrentTenant>();
        public EmbeddingsMetrics Metrics => field ??= new EmbeddingsMetrics(MeterFactory);
        public IOptions<GranitIndexingEmbeddingsOptions> OptionsAccessor => Microsoft.Extensions.Options.Options.Create(Options);

        public EmbeddingIndexer<int> Build() =>
            new(Inner, Factory, OptionsAccessor, Metrics, Tenant, NullLogger<EmbeddingIndexer<int>>.Instance);
    }

    private sealed class RecordingIndexer : IIndexer<int>
    {
        public List<IndexedEntry<int>> Indexed { get; } = [];
        public List<(int Key, Guid? Tenant)> Removed { get; } = [];

        public Task IndexAsync(IndexedEntry<int> entry, CancellationToken cancellationToken = default)
        {
            Indexed.Add(entry);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(int key, Guid? tenantId, CancellationToken cancellationToken = default)
        {
            Removed.Add((key, tenantId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGeneratorFactory : IAIEmbeddingGeneratorFactory
    {
        public FakeGenerator Generator { get; set; } = FakeGenerator.Empty();
        public int CreateCount { get; private set; }
        public string? LastWorkspace { get; private set; }

        public Task<IEmbeddingGenerator<string, Embedding<float>>> CreateAsync(
            string? workspaceName = null, CancellationToken cancellationToken = default)
        {
            CreateCount++;
            LastWorkspace = workspaceName;
            return Task.FromResult<IEmbeddingGenerator<string, Embedding<float>>>(Generator);
        }
    }

    private sealed class FakeGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly Func<GeneratedEmbeddings<Embedding<float>>> _behavior;

        private FakeGenerator(Func<GeneratedEmbeddings<Embedding<float>>> behavior) => _behavior = behavior;

        public bool Disposed { get; private set; }

        public static FakeGenerator Returning(float[] vector) =>
            new(() => [new Embedding<float>(vector)]);

        public static FakeGenerator Empty() => new(() => []);

        public static FakeGenerator Throwing(Exception ex) => new(() => throw ex);

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_behavior());

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() => Disposed = true;
    }
}
