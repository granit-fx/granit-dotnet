using System.Diagnostics.Metrics;
using Granit.AI.Diagnostics;
using Granit.AI.Internal;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class UsageTrackingEmbeddingGeneratorTests
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _inner =
        Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();

    private readonly IAIUsageTracker _usageTracker = Substitute.For<IAIUsageTracker>();
    private readonly IAIUsageRecordFactory _usageRecordFactory = Substitute.For<IAIUsageRecordFactory>();

    private static readonly AIWorkspace Workspace = new()
    {
        Key = "ws",
        Provider = "OpenAI",
        Model = "text-embedding-3-small",
    };

    public UsageTrackingEmbeddingGeneratorTests() =>
        _usageRecordFactory
            .Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan?>())
            .Returns(callInfo => new AIUsageRecord
            {
                Id = Guid.NewGuid(),
                WorkspaceName = callInfo.ArgAt<string>(0),
                Provider = callInfo.ArgAt<string>(1),
                Model = callInfo.ArgAt<string>(2),
                InputTokens = callInfo.ArgAt<int>(3),
                OutputTokens = callInfo.ArgAt<int>(4),
                Timestamp = DateTimeOffset.UtcNow,
            });

    private UsageTrackingEmbeddingGenerator CreateSut() =>
        new(_inner, "ws", Workspace, _usageTracker, _usageRecordFactory,
            new AIMetrics(new StubMeterFactory()), TimeProvider.System);

    [Fact]
    public async Task GenerateAsync_WithUsage_StampsOneRecordWithZeroOutputTokens()
    {
        GeneratedEmbeddings<Embedding<float>> embeddings = new([new Embedding<float>(new float[] { 0.1f })])
        {
            Usage = new UsageDetails { InputTokenCount = 42 },
        };
        _inner
            .GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(embeddings);

        await CreateSut().GenerateAsync(["hello"], cancellationToken: TestContext.Current.CancellationToken);

        await _usageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.InputTokens == 42 && r.OutputTokens == 0 && r.WorkspaceName == "ws"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_WithoutUsage_StampsNothing()
    {
        GeneratedEmbeddings<Embedding<float>> embeddings = new([new Embedding<float>(new float[] { 0.1f })]);
        _inner
            .GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(embeddings);

        await CreateSut().GenerateAsync(["hello"], cancellationToken: TestContext.Current.CancellationToken);

        await _usageTracker.DidNotReceive().RecordAsync(Arg.Any<AIUsageRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_ReturnsInnerResultUnchanged()
    {
        GeneratedEmbeddings<Embedding<float>> embeddings = new([new Embedding<float>(new float[] { 0.5f })])
        {
            Usage = new UsageDetails { InputTokenCount = 3 },
        };
        _inner
            .GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(embeddings);

        GeneratedEmbeddings<Embedding<float>> result =
            await CreateSut().GenerateAsync(["hello"], cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(embeddings);
    }

    private sealed class StubMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);

        public void Dispose() { }
    }
}
