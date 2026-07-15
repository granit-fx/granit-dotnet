using System.Diagnostics.Metrics;
using Granit.AI.Diagnostics;
using Granit.AI.Exceptions;
using Granit.AI.Internal;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class DefaultAIChatClientFactoryTests
{
    private readonly IAIWorkspaceProvider _workspaceProvider = Substitute.For<IAIWorkspaceProvider>();
    private readonly IAIUsageTracker _usageTracker = Substitute.For<IAIUsageTracker>();
    private readonly IAIUsageRecordFactory _usageRecordFactory = Substitute.For<IAIUsageRecordFactory>();
    private readonly IOptions<GranitAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new GranitAIOptions { DefaultWorkspace = "default" });

    private static AIWorkspace CreateWorkspace(string key = "test", string provider = "OpenAI") =>
        new()
        {
            Key = key,
            Provider = provider,
            Model = "gpt-4o",
        };

    private DefaultAIChatClientFactory CreateFactory(params IAIProviderFactory[] providerFactories) =>
        new(
            _workspaceProvider,
            providerFactories,
            _usageTracker,
            _usageRecordFactory,
            new AIMetrics(new StubMeterFactory()),
            TimeProvider.System,
            NullLogger<UsageTrackingChatClient>.Instance,
            _options);

    [Fact]
    public async Task CreateAsync_KnownWorkspaceAndProvider_ReturnsUsageTrackedChatClient()
    {
        AIWorkspace workspace = CreateWorkspace();
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        IChatClient mockClient = Substitute.For<IChatClient>();
        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateChatClientAsync(workspace, Arg.Any<CancellationToken>()).Returns(mockClient);

        DefaultAIChatClientFactory factory = CreateFactory(providerFactory);

        IChatClient result = await factory.CreateAsync("test", TestContext.Current.CancellationToken);

        result.ShouldBeOfType<UsageTrackingChatClient>();
    }

    [Fact]
    public async Task CreateAsync_ReturnedClient_StampsUsageOnResponse()
    {
        AIWorkspace workspace = CreateWorkspace();
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        IChatClient mockClient = Substitute.For<IChatClient>();
        mockClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "hi"))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            });

        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateChatClientAsync(workspace, Arg.Any<CancellationToken>()).Returns(mockClient);

        _usageRecordFactory
            .Create("test", "OpenAI", "gpt-4o", 10, 5, Arg.Any<TimeSpan?>())
            .Returns(new AIUsageRecord
            {
                Id = Guid.NewGuid(),
                WorkspaceName = "test",
                Provider = "OpenAI",
                Model = "gpt-4o",
                Timestamp = DateTimeOffset.UtcNow,
            });

        DefaultAIChatClientFactory factory = CreateFactory(providerFactory);
        IChatClient client = await factory.CreateAsync("test", TestContext.Current.CancellationToken);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            cancellationToken: TestContext.Current.CancellationToken);

        await _usageTracker.Received(1).RecordAsync(Arg.Any<AIUsageRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_NullWorkspaceName_UsesDefault()
    {
        AIWorkspace workspace = CreateWorkspace("default");
        _workspaceProvider.GetAsync("default", Arg.Any<CancellationToken>()).Returns(workspace);

        IChatClient mockClient = Substitute.For<IChatClient>();
        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateChatClientAsync(workspace, Arg.Any<CancellationToken>()).Returns(mockClient);

        DefaultAIChatClientFactory factory = CreateFactory(providerFactory);

        IChatClient result = await factory.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        await providerFactory.Received(1).CreateChatClientAsync(workspace, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WorkspaceNotFound_ThrowsAIWorkspaceNotFoundException()
    {
        _workspaceProvider.GetAsync("missing", Arg.Any<CancellationToken>()).Returns((AIWorkspace?)null);

        DefaultAIChatClientFactory factory = CreateFactory();

        AIWorkspaceNotFoundException exception = await Should.ThrowAsync<AIWorkspaceNotFoundException>(
            () => factory.CreateAsync("missing", TestContext.Current.CancellationToken));

        exception.WorkspaceName.ShouldBe("missing");
    }

    [Fact]
    public async Task CreateAsync_ProviderNotRegistered_ThrowsAIProviderNotRegisteredException()
    {
        AIWorkspace workspace = CreateWorkspace(provider: "UnknownProvider");
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        DefaultAIChatClientFactory factory = CreateFactory();

        AIProviderNotRegisteredException exception = await Should.ThrowAsync<AIProviderNotRegisteredException>(
            () => factory.CreateAsync("test", TestContext.Current.CancellationToken));

        exception.ProviderName.ShouldBe("UnknownProvider");
    }

    private sealed class StubMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);

        public void Dispose() { }
    }
}
