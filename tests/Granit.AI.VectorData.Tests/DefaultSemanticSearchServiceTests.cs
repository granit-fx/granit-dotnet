using Granit.AI;
using Granit.AI.VectorData;
using Granit.AI.VectorData.Internal;
using Granit.AI.VectorData.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.AI.VectorData.Tests;

public sealed class DefaultSemanticSearchServiceTests
{
    private readonly IAIEmbeddingGeneratorFactory _embeddingGeneratorFactory = Substitute.For<IAIEmbeddingGeneratorFactory>();
    private readonly IVectorCollectionFactory _vectorCollectionFactory = Substitute.For<IVectorCollectionFactory>();
    private readonly IOptions<VectorDataOptions> _options = Microsoft.Extensions.Options.Options.Create(new VectorDataOptions());
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
    private readonly IVectorCollection<TextVectorRecord> _vectorCollection = Substitute.For<IVectorCollection<TextVectorRecord>>();
    private readonly DefaultSemanticSearchService _sut;

    public DefaultSemanticSearchServiceTests()
    {
        _embeddingGeneratorFactory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(_embeddingGenerator);
        _vectorCollectionFactory.GetCollection<TextVectorRecord>(Arg.Any<string>()).Returns(_vectorCollection);

        _sut = new DefaultSemanticSearchService(
            _embeddingGeneratorFactory,
            _vectorCollectionFactory,
            _options,
            NullLogger<DefaultSemanticSearchService>.Instance);
    }

    [Fact]
    public async Task IndexAsync_GeneratesEmbeddingAndUpsertsRecord()
    {
        float[] vector = [0.1f, 0.2f, 0.3f];
        var embedding = new Embedding<float>(vector);
        var embeddings = new GeneratedEmbeddings<Embedding<float>>([embedding]);

        _embeddingGenerator
            .GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(embeddings);

        await _sut.IndexAsync("test-collection", "doc-1", "Hello world", TestContext.Current.CancellationToken);

        await _embeddingGeneratorFactory.Received(1).CreateAsync("default", Arg.Any<CancellationToken>());
        await _vectorCollection.Received(1).UpsertAsync(
            Arg.Is<TextVectorRecord>(r => r.Key == "doc-1" && r.Text == "Hello world"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_GeneratesEmbeddingAndSearchesCollection()
    {
        float[] vector = [0.4f, 0.5f, 0.6f];
        var embedding = new Embedding<float>(vector);
        var embeddings = new GeneratedEmbeddings<Embedding<float>>([embedding]);

        _embeddingGenerator
            .GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(embeddings);

        List<VectorSearchResult<TextVectorRecord>> searchResults =
        [
            new(new TextVectorRecord { Key = "doc-1", Text = "Hello world", Vector = new float[] { 0.1f, 0.2f, 0.3f } }, 0.95),
            new(new TextVectorRecord { Key = "doc-2", Text = "Goodbye world", Vector = new float[] { 0.4f, 0.5f, 0.6f } }, 0.80),
        ];

        _vectorCollection
            .SearchAsync(Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(searchResults);

        IReadOnlyList<SemanticSearchResult> results = await _sut.SearchAsync("test-collection", "search query", 5, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
        results[0].Key.ShouldBe("doc-1");
        results[0].Score.ShouldBe(0.95);
        results[0].Text.ShouldBe("Hello world");

        await _embeddingGeneratorFactory.Received(1).CreateAsync("default", Arg.Any<CancellationToken>());
        await _vectorCollection.Received(1).SearchAsync(
            Arg.Any<ReadOnlyMemory<float>>(),
            5,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_UsesDefaultLimitFromOptionsWhenLimitIsZero()
    {
        float[] vector = [0.1f, 0.2f, 0.3f];
        var embedding = new Embedding<float>(vector);
        var embeddings = new GeneratedEmbeddings<Embedding<float>>([embedding]);

        _embeddingGenerator
            .GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(embeddings);

        _vectorCollection
            .SearchAsync(Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.SearchAsync("test-collection", "query", limit: 0, TestContext.Current.CancellationToken);

        await _vectorCollection.Received(1).SearchAsync(
            Arg.Any<ReadOnlyMemory<float>>(),
            10,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexAsync_ThrowsOnNullCollectionName()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.IndexAsync(null!, "key", "text", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchAsync_ThrowsOnNullQuery()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SearchAsync("collection", null!, cancellationToken: TestContext.Current.CancellationToken));
    }
}
