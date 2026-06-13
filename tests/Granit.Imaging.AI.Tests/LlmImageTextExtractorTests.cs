using Granit.AI;
using Granit.AI.Workspaces;
using Granit.Imaging.AI.Internal;
using Granit.Imaging.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Imaging.AI.Tests;

public sealed class LlmImageTextExtractorTests
{
    private static readonly ReadOnlyMemory<byte> Image = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IAIWorkspaceProvider _workspaceProvider = Substitute.For<IAIWorkspaceProvider>();
    private readonly IAIWorkspaceCapabilityResolver _capabilityResolver = Substitute.For<IAIWorkspaceCapabilityResolver>();
    private readonly IAIUsageRecordFactory _usageRecordFactory = Substitute.For<IAIUsageRecordFactory>();
    private readonly IAIUsageTracker _usageTracker = Substitute.For<IAIUsageTracker>();

    private static AIWorkspace Workspace(string name, string provider = "OpenAI", string model = "gpt-4o") =>
        new() { Name = name, Provider = provider, Model = model };

    private void SetupResponse(string text, int? input = 10, int? output = 4)
    {
        ChatResponse response = new(new ChatMessage(ChatRole.Assistant, text));
        if (input is not null || output is not null)
        {
            response.Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output };
        }

        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);
        _chatClientFactory.CreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_chatClient);
    }

    private LlmImageTextExtractor CreateExtractor(string? workspaceName) =>
        new(_chatClientFactory, _workspaceProvider, _capabilityResolver, _usageRecordFactory, _usageTracker,
            MsOptions.Create(new ImagingAIOptions { WorkspaceName = workspaceName, TimeoutSeconds = 30 }),
            NullLogger<LlmImageTextExtractor>.Instance);

    [Fact]
    public async Task Uses_the_explicitly_configured_workspace_and_stamps_usage()
    {
        _workspaceProvider.GetAsync("vision", Arg.Any<CancellationToken>()).Returns(Workspace("vision"));
        SetupResponse("INVOICE #42");
        _usageRecordFactory.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan?>())
            .Returns(new AIUsageRecord { Id = Guid.Empty, WorkspaceName = "vision", Provider = "OpenAI", Model = "gpt-4o", Timestamp = DateTimeOffset.UnixEpoch });

        LlmImageTextExtractor extractor = CreateExtractor("vision");

        ImageTextExtractionResult? result = await extractor.ExtractTextAsync(Image, "image/png", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Text.ShouldBe("INVOICE #42");
        result.Workspace.ShouldBe("vision");
        await _usageTracker.Received(1).RecordAsync(Arg.Any<AIUsageRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_null_when_no_workspace_is_configured_or_discoverable()
    {
        _workspaceProvider.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        LlmImageTextExtractor extractor = CreateExtractor(workspaceName: null);

        ImageTextExtractionResult? result = await extractor.ExtractTextAsync(Image, "image/png", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _chatClientFactory.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Auto_discovers_the_first_vision_capable_workspace()
    {
        AIWorkspace textOnly = Workspace("chat", model: "gpt-text");
        AIWorkspace vision = Workspace("vision-ws", model: "gpt-4o");
        _workspaceProvider.GetAllAsync(Arg.Any<CancellationToken>()).Returns([textOnly, vision]);
        _capabilityResolver.ResolveAsync("OpenAI", "gpt-text", Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { Vision = false });
        _capabilityResolver.ResolveAsync("OpenAI", "gpt-4o", Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { Vision = true });
        SetupResponse("text");

        LlmImageTextExtractor extractor = CreateExtractor(workspaceName: null);

        ImageTextExtractionResult? result = await extractor.ExtractTextAsync(Image, "image/png", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Workspace.ShouldBe("vision-ws");
    }
}
