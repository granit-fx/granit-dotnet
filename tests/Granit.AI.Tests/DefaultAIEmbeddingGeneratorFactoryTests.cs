using System.Diagnostics.Metrics;
using Granit.AI.Diagnostics;
using Granit.AI.Exceptions;
using Granit.AI.Internal;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class DefaultAIEmbeddingGeneratorFactoryTests
{
    private readonly IAIWorkspaceProvider _workspaceProvider = Substitute.For<IAIWorkspaceProvider>();
    private readonly IAIUsageTracker _usageTracker = Substitute.For<IAIUsageTracker>();
    private readonly IAIUsageRecordFactory _usageRecordFactory = Substitute.For<IAIUsageRecordFactory>();
    private readonly IOptions<GranitAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new GranitAIOptions { DefaultWorkspace = "default" });

    private static AIWorkspace CreateWorkspace(string name = "test", string provider = "OpenAI") =>
        new() { Key = name, Provider = provider, Model = "text-embedding-3-small" };

    private DefaultAIEmbeddingGeneratorFactory CreateFactory(params IAIProviderFactory[] providerFactories) =>
        new(
            _workspaceProvider,
            providerFactories,
            _usageTracker,
            _usageRecordFactory,
            new AIMetrics(new StubMeterFactory()),
            TimeProvider.System,
            _options);

    [Fact]
    public async Task CreateAsync_KnownWorkspaceAndProvider_ReturnsUsageTrackedGenerator()
    {
        AIWorkspace workspace = CreateWorkspace();
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        IEmbeddingGenerator<string, Embedding<float>> mockGenerator =
            Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateEmbeddingGeneratorAsync(workspace, Arg.Any<CancellationToken>()).Returns(mockGenerator);

        DefaultAIEmbeddingGeneratorFactory factory = CreateFactory(providerFactory);

        IEmbeddingGenerator<string, Embedding<float>> result =
            await factory.CreateAsync("test", TestContext.Current.CancellationToken);

        result.ShouldBeOfType<UsageTrackingEmbeddingGenerator>();
    }

    [Fact]
    public async Task CreateAsync_ReturnedGenerator_StampsUsageOnGeneration()
    {
        AIWorkspace workspace = CreateWorkspace();
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        GeneratedEmbeddings<Embedding<float>> embeddings = new([new Embedding<float>(new float[] { 0.1f })])
        {
            Usage = new UsageDetails { InputTokenCount = 7 },
        };

        IEmbeddingGenerator<string, Embedding<float>> mockGenerator =
            Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator
            .GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(embeddings);

        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateEmbeddingGeneratorAsync(workspace, Arg.Any<CancellationToken>()).Returns(mockGenerator);

        _usageRecordFactory
            .Create("test", "OpenAI", "text-embedding-3-small", 7, 0, Arg.Any<TimeSpan?>())
            .Returns(new AIUsageRecord
            {
                Id = Guid.NewGuid(),
                WorkspaceName = "test",
                Provider = "OpenAI",
                Model = "text-embedding-3-small",
                Timestamp = DateTimeOffset.UtcNow,
            });

        DefaultAIEmbeddingGeneratorFactory factory = CreateFactory(providerFactory);
        IEmbeddingGenerator<string, Embedding<float>> generator =
            await factory.CreateAsync("test", TestContext.Current.CancellationToken);

        await generator.GenerateAsync(["hello"], cancellationToken: TestContext.Current.CancellationToken);

        await _usageTracker.Received(1).RecordAsync(Arg.Any<AIUsageRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_NullWorkspaceName_UsesDefault()
    {
        AIWorkspace workspace = CreateWorkspace("default");
        _workspaceProvider.GetAsync("default", Arg.Any<CancellationToken>()).Returns(workspace);

        IEmbeddingGenerator<string, Embedding<float>> mockGenerator =
            Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateEmbeddingGeneratorAsync(workspace, Arg.Any<CancellationToken>()).Returns(mockGenerator);

        DefaultAIEmbeddingGeneratorFactory factory = CreateFactory(providerFactory);

        IEmbeddingGenerator<string, Embedding<float>> result =
            await factory.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        await providerFactory.Received(1).CreateEmbeddingGeneratorAsync(workspace, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WorkspaceNotFound_ThrowsAIWorkspaceNotFoundException()
    {
        _workspaceProvider.GetAsync("missing", Arg.Any<CancellationToken>()).Returns((AIWorkspace?)null);

        DefaultAIEmbeddingGeneratorFactory factory = CreateFactory();

        AIWorkspaceNotFoundException exception = await Should.ThrowAsync<AIWorkspaceNotFoundException>(
            () => factory.CreateAsync("missing", TestContext.Current.CancellationToken));

        exception.WorkspaceName.ShouldBe("missing");
    }

    [Fact]
    public async Task CreateAsync_ProviderNotRegistered_ThrowsAIProviderNotRegisteredException()
    {
        AIWorkspace workspace = CreateWorkspace(provider: "UnknownProvider");
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        DefaultAIEmbeddingGeneratorFactory factory = CreateFactory();

        AIProviderNotRegisteredException exception = await Should.ThrowAsync<AIProviderNotRegisteredException>(
            () => factory.CreateAsync("test", TestContext.Current.CancellationToken));

        exception.ProviderName.ShouldBe("UnknownProvider");
    }

    [Fact]
    public async Task CreateAsync_ProviderReturnsNullGenerator_ThrowsInvalidOperationException()
    {
        AIWorkspace workspace = CreateWorkspace();
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateEmbeddingGeneratorAsync(workspace, Arg.Any<CancellationToken>()).Returns((IEmbeddingGenerator<string, Embedding<float>>?)null);

        DefaultAIEmbeddingGeneratorFactory factory = CreateFactory(providerFactory);

        await Should.ThrowAsync<InvalidOperationException>(
            () => factory.CreateAsync("test", TestContext.Current.CancellationToken));
    }

    private sealed class StubMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);

        public void Dispose() { }
    }
}
