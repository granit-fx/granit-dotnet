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

public sealed class HybridSearchBackendTests
{
    private static readonly float[] QueryVector = [0.4f, 0.5f, 0.6f];

    [Fact]
    public void Name_combines_the_inner_backend_names()
    {
        Harness h = new();
        h.Build().Name.ShouldBe("hybrid(lexical+vector)");
    }

    [Fact]
    public async Task SearchAsync_queries_both_channels_with_the_default_pool_depth()
    {
        Harness h = new();
        h.Options.RrfFetchPoolSize = 200;
        h.Factory.Generator = FakeGenerator.Returning(QueryVector);
        h.Lexical.Page = Page([(1, 1.0)]);
        h.Vector.Page = Page([(2, 1.0)]);
        using MetricCollector<long> queries = new(h.MeterFactory.Meter, "granit.indexing.embeddings.hybrid.queries");
        using MetricCollector<double> latency = new(h.MeterFactory.Meter, "granit.indexing.embeddings.hybrid.latency");

        BackendSearchPage<int, string> page = await h.Build().SearchAsync(
            new SearchRequest("q"), offset: 0, limit: 10, TestContext.Current.CancellationToken);

        // poolSize = max(200, (0 + 10) * 2) = 200; both channels fetch the deep pool from offset 0.
        h.Lexical.Calls.ShouldHaveSingleItem().ShouldBe((0, 200));
        h.Vector.Calls.ShouldHaveSingleItem().ShouldBe((0, 200));
        h.Vector.LastEmbedding!.Value.ToArray().ShouldBe(QueryVector);
        page.Hits.Select(x => x.Key).ShouldBe([1, 2], ignoreOrder: true);
        queries.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
        latency.GetMeasurementSnapshot().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_expands_the_pool_for_deep_pagination()
    {
        Harness h = new();
        h.Options.RrfFetchPoolSize = 4;
        h.Factory.Generator = FakeGenerator.Returning(QueryVector);

        await h.Build().SearchAsync(
            new SearchRequest("q"), offset: 10, limit: 5, TestContext.Current.CancellationToken);

        // poolSize = max(4, (10 + 5) * 2) = 30.
        h.Lexical.Calls.ShouldHaveSingleItem().ShouldBe((0, 30));
        h.Vector.Calls.ShouldHaveSingleItem().ShouldBe((0, 30));
    }

    [Fact]
    public async Task SearchAsync_slices_the_fused_results_to_offset_and_limit()
    {
        Harness h = new();
        h.Factory.Generator = FakeGenerator.Returning(QueryVector);
        // Vector empty → fused order follows the lexical ranking 1..5.
        h.Lexical.Page = Page([(1, 0.9), (2, 0.8), (3, 0.7), (4, 0.6), (5, 0.5)]);
        h.Vector.Page = Page([]);

        BackendSearchPage<int, string> page = await h.Build().SearchAsync(
            new SearchRequest("q"), offset: 1, limit: 2, TestContext.Current.CancellationToken);

        page.Hits.Select(x => x.Key).ShouldBe([2, 3]);
        page.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_records_a_vector_backend_miss_when_the_semantic_channel_is_empty()
    {
        Harness h = new();
        h.Factory.Generator = FakeGenerator.Returning(QueryVector);
        h.Lexical.Page = Page([(1, 1.0)]);
        h.Vector.Page = Page([]);
        using MetricCollector<long> misses = new(h.MeterFactory.Meter, "granit.indexing.embeddings.vector.misses");

        await h.Build().SearchAsync(new SearchRequest("q"), offset: 0, limit: 10, TestContext.Current.CancellationToken);

        misses.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
    }

    [Fact]
    public async Task SearchAsync_falls_back_to_lexical_only_when_query_embedding_fails()
    {
        Harness h = new();
        h.Factory.Generator = FakeGenerator.Throwing(new InvalidOperationException("provider down"));
        h.Lexical.Page = Page([(1, 1.0)]);
        using MetricCollector<long> failed = new(h.MeterFactory.Meter, "granit.indexing.embeddings.failed");

        BackendSearchPage<int, string> page = await h.Build().SearchAsync(
            new SearchRequest("q"), offset: 3, limit: 7, TestContext.Current.CancellationToken);

        // Lexical receives the ORIGINAL page coordinates; the vector channel is never touched.
        h.Lexical.Calls.ShouldHaveSingleItem().ShouldBe((3, 7));
        h.Vector.Calls.ShouldBeEmpty();
        page.Hits.ShouldHaveSingleItem().Key.ShouldBe(1);
        failed.GetMeasurementSnapshot().ShouldHaveSingleItem().Tags["reason"].ShouldBe("transport");
    }

    [Fact]
    public async Task SearchAsync_reports_more_results_when_an_inner_page_has_more()
    {
        Harness h = new();
        h.Factory.Generator = FakeGenerator.Returning(QueryVector);
        h.Lexical.Page = new([new SearchHit<int, string>(1, "1", 1.0)], HasMore: true);
        h.Vector.Page = Page([]);

        BackendSearchPage<int, string> page = await h.Build().SearchAsync(
            new SearchRequest("q"), offset: 0, limit: 10, TestContext.Current.CancellationToken);

        // Only one fused hit fits the window, but the lexical channel signalled more rows upstream.
        page.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_does_not_swallow_cancellation()
    {
        Harness h = new();
        h.Factory.Generator = FakeGenerator.Throwing(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => h.Build().SearchAsync(new SearchRequest("q"), offset: 0, limit: 10, TestContext.Current.CancellationToken));

        h.Lexical.Calls.ShouldBeEmpty();
        h.Vector.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_rejects_a_null_request()
    {
        Harness h = new();
        await Should.ThrowAsync<ArgumentNullException>(
            () => h.Build().SearchAsync(null!, offset: 0, limit: 10, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_rejects_null_arguments()
    {
        Harness h = new();
        IOptions<GranitIndexingEmbeddingsOptions> opts = h.OptionsAccessor;
        NullLogger<HybridSearchBackend<int, string>> logger = NullLogger<HybridSearchBackend<int, string>>.Instance;

        Should.Throw<ArgumentNullException>(() => new HybridSearchBackend<int, string>(null!, h.Vector, h.Factory, opts, h.Metrics, h.Tenant, TimeProvider.System, logger));
        Should.Throw<ArgumentNullException>(() => new HybridSearchBackend<int, string>(h.Lexical, null!, h.Factory, opts, h.Metrics, h.Tenant, TimeProvider.System, logger));
        Should.Throw<ArgumentNullException>(() => new HybridSearchBackend<int, string>(h.Lexical, h.Vector, null!, opts, h.Metrics, h.Tenant, TimeProvider.System, logger));
        Should.Throw<ArgumentNullException>(() => new HybridSearchBackend<int, string>(h.Lexical, h.Vector, h.Factory, null!, h.Metrics, h.Tenant, TimeProvider.System, logger));
        Should.Throw<ArgumentNullException>(() => new HybridSearchBackend<int, string>(h.Lexical, h.Vector, h.Factory, opts, null!, h.Tenant, TimeProvider.System, logger));
        Should.Throw<ArgumentNullException>(() => new HybridSearchBackend<int, string>(h.Lexical, h.Vector, h.Factory, opts, h.Metrics, null!, TimeProvider.System, logger));
        Should.Throw<ArgumentNullException>(() => new HybridSearchBackend<int, string>(h.Lexical, h.Vector, h.Factory, opts, h.Metrics, h.Tenant, null!, logger));
        Should.Throw<ArgumentNullException>(() => new HybridSearchBackend<int, string>(h.Lexical, h.Vector, h.Factory, opts, h.Metrics, h.Tenant, TimeProvider.System, null!));
    }

    private static BackendSearchPage<int, string> Page(params (int Key, double Score)[] hits) =>
        new([.. hits.Select(t => new SearchHit<int, string>(t.Key, t.Key.ToString(), t.Score))], HasMore: false);

    private sealed class Harness
    {
        public TestMeterFactory MeterFactory { get; } = new();
        public RecordingLexical Lexical { get; } = new();
        public RecordingVector Vector { get; } = new();
        public FakeGeneratorFactory Factory { get; } = new();
        public GranitIndexingEmbeddingsOptions Options { get; } = new() { Dimensions = 3, EmbeddingModelId = "test-model" };
        public ICurrentTenant Tenant { get; } = Substitute.For<ICurrentTenant>();
        public EmbeddingsMetrics Metrics => field ??= new EmbeddingsMetrics(MeterFactory);
        public IOptions<GranitIndexingEmbeddingsOptions> OptionsAccessor => Microsoft.Extensions.Options.Options.Create(Options);

        public HybridSearchBackend<int, string> Build() =>
            new(Lexical, Vector, Factory, OptionsAccessor, Metrics, Tenant, TimeProvider.System,
                NullLogger<HybridSearchBackend<int, string>>.Instance);
    }

    private sealed class RecordingLexical : ISearchBackend<int, string>
    {
        public string Name => "lexical";
        public List<(int Offset, int Limit)> Calls { get; } = [];
        public BackendSearchPage<int, string> Page { get; set; } = new([], false);

        public Task<BackendSearchPage<int, string>> SearchAsync(
            SearchRequest request, int offset, int limit, CancellationToken cancellationToken = default)
        {
            Calls.Add((offset, limit));
            return Task.FromResult(Page);
        }
    }

    private sealed class RecordingVector : IVectorSearchBackend<int, string>
    {
        public string Name => "vector";
        public List<(int Offset, int Limit)> Calls { get; } = [];
        public ReadOnlyMemory<float>? LastEmbedding { get; private set; }
        public BackendSearchPage<int, string> Page { get; set; } = new([], false);

        public Task<BackendSearchPage<int, string>> SearchAsync(
            ReadOnlyMemory<float> queryEmbedding, SearchRequest request, int offset, int limit,
            CancellationToken cancellationToken = default)
        {
            LastEmbedding = queryEmbedding;
            Calls.Add((offset, limit));
            return Task.FromResult(Page);
        }
    }

    private sealed class FakeGeneratorFactory : IAIEmbeddingGeneratorFactory
    {
        public FakeGenerator Generator { get; set; } = FakeGenerator.Returning(QueryVector);

        public Task<IEmbeddingGenerator<string, Embedding<float>>> CreateAsync(
            string? workspaceName = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEmbeddingGenerator<string, Embedding<float>>>(Generator);
    }

    private sealed class FakeGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly Func<GeneratedEmbeddings<Embedding<float>>> _behavior;

        private FakeGenerator(Func<GeneratedEmbeddings<Embedding<float>>> behavior) => _behavior = behavior;

        public static FakeGenerator Returning(float[] vector) => new(() => [new Embedding<float>(vector)]);

        public static FakeGenerator Throwing(Exception ex) => new(() => throw ex);

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_behavior());

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
