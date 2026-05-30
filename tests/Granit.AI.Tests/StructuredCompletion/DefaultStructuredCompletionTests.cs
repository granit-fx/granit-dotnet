using System.Diagnostics.Metrics;
using Granit.AI.Diagnostics;
using Granit.AI.Internal;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.AI.Tests.StructuredCompletion;

public sealed record SeoExtraction
{
    public string? Title { get; init; }
    public string? Description { get; init; }
}

public sealed class DefaultStructuredCompletionTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IAIWorkspaceProvider _workspaceProvider = Substitute.For<IAIWorkspaceProvider>();
    private readonly IAIWorkspaceCapabilityResolver _capabilityResolver = Substitute.For<IAIWorkspaceCapabilityResolver>();
    private readonly IAIQuotaGuard _quotaGuard = Substitute.For<IAIQuotaGuard>();
    private readonly IAIUsageTracker _usageTracker = Substitute.For<IAIUsageTracker>();
    private readonly IAIUsageRecordFactory _usageRecordFactory = Substitute.For<IAIUsageRecordFactory>();

    private readonly DefaultStructuredCompletion _sut;

    public DefaultStructuredCompletionTests()
    {
        _workspaceProvider
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AIWorkspace { Name = "test", Provider = "OpenAI", Model = "gpt-4o-mini" });

        _capabilityResolver
            .ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { StructuredOutput = true });

        _quotaGuard.CheckAsync(Arg.Any<CancellationToken>()).Returns(AIQuotaResult.Allowed);

        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _sut = new DefaultStructuredCompletion(
            _chatClientFactory,
            _workspaceProvider,
            _capabilityResolver,
            _quotaGuard,
            _usageTracker,
            _usageRecordFactory,
            new AIMetrics(new StubMeterFactory()),
            Microsoft.Extensions.Options.Options.Create(new StructuredCompletionOptions { TimeoutSeconds = 30 }),
            Microsoft.Extensions.Options.Options.Create(new GranitAIOptions { DefaultWorkspace = "test" }),
            NullLogger<DefaultStructuredCompletion>.Instance);
    }

    private void RespondWith(ChatResponse response) =>
        _chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(response);

    private static StructuredCompletionRequest Request(string? instruction = null) =>
        new() { Instruction = instruction, Content = "The page body." };

    [Fact]
    public async Task CompleteAsync_ValidResponse_ReturnsSucceededWithValueAndModelId()
    {
        RespondWith(new ChatResponse(new ChatMessage(ChatRole.Assistant,
            """{"title":"Home","description":"Welcome"}"""))
        {
            FinishReason = ChatFinishReason.Stop,
            ModelId = "gpt-4o-mini-2024-07-18",
        });

        StructuredCompletionResult<SeoExtraction> result =
            await _sut.CompleteAsync<SeoExtraction>(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(StructuredCompletionStatus.Succeeded);
        result.Value.ShouldNotBeNull();
        result.Value.Title.ShouldBe("Home");
        result.Value.Description.ShouldBe("Welcome");
        result.ModelId.ShouldBe("gpt-4o-mini-2024-07-18");
        result.FinishReason.ShouldBe(ChatFinishReason.Stop);
    }

    [Fact]
    public async Task CompleteAsync_StructuredOutputSupported_PassesForJsonSchemaResponseFormat()
    {
        ChatOptions? captured = null;
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Do<ChatOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, """{"title":"H"}"""))
            {
                FinishReason = ChatFinishReason.Stop,
            });

        await _sut.CompleteAsync<SeoExtraction>(Request(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>();
    }

    [Fact]
    public async Task CompleteAsync_StructuredOutputUnsupported_UsesNoResponseFormat_SchemaInPrompt_AndStripsFences()
    {
        _capabilityResolver
            .ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { StructuredOutput = false });

        ChatOptions? captured = null;
        List<ChatMessage>? messages = null;
        _chatClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(m => messages = m.ToList()),
                Arg.Do<ChatOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                "```json\n{\"title\":\"Home\"}\n```"))
            {
                FinishReason = ChatFinishReason.Stop,
            });

        StructuredCompletionResult<SeoExtraction> result =
            await _sut.CompleteAsync<SeoExtraction>(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(StructuredCompletionStatus.Succeeded);
        result.Value!.Title.ShouldBe("Home");
        captured.ShouldBeNull(); // no provider-enforced ResponseFormat on the fallback path
        messages.ShouldNotBeNull();
        messages.Single().Text.ShouldContain("schema"); // schema injected into the prompt
    }

    [Fact]
    public async Task CompleteAsync_ContentFilterFinishReason_ReturnsModelRefused()
    {
        RespondWith(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty))
        {
            FinishReason = ChatFinishReason.ContentFilter,
        });

        StructuredCompletionResult<SeoExtraction> result =
            await _sut.CompleteAsync<SeoExtraction>(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(StructuredCompletionStatus.ModelRefused);
        result.Value.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAsync_InvalidJson_ReturnsSchemaViolation_WithoutLeakingResponse()
    {
        RespondWith(new ChatResponse(new ChatMessage(ChatRole.Assistant,
            "not json containing SSN 123-45-6789")));

        StructuredCompletionResult<SeoExtraction> result =
            await _sut.CompleteAsync<SeoExtraction>(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(StructuredCompletionStatus.SchemaViolation);
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotContain("123-45-6789");
    }

    [Fact]
    public async Task CompleteAsync_ProviderThrows_ReturnsTransportFailure_WithoutLeakingMessage()
    {
        const string leaky = "Provider error: prompt was 'SSN 123-45-6789'";
        _chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(leaky));

        StructuredCompletionResult<SeoExtraction> result =
            await _sut.CompleteAsync<SeoExtraction>(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(StructuredCompletionStatus.TransportFailure);
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotContain("123-45-6789");
        result.ErrorMessage.ShouldNotContain("prompt was");
    }

    [Fact]
    public async Task CompleteAsync_CustomInstructionAndContext_FlowIntoPromptWithContentInDataBlock()
    {
        const string instruction = "Generate SEO metadata for the page below in French.";
        List<ChatMessage>? messages = null;
        _chatClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(m => messages = m.ToList()),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, """{"title":"H"}"""))
            {
                FinishReason = ChatFinishReason.Stop,
            });

        await _sut.CompleteAsync<SeoExtraction>(
            new StructuredCompletionRequest
            {
                Instruction = instruction,
                Content = "Editor body.",
                ContentLabel = "Page content",
                Context = [new("Title", "Home page"), new("Locale", "fr-FR")],
            },
            TestContext.Current.CancellationToken);

        messages.ShouldNotBeNull();
        string prompt = messages.Single().Text;
        prompt.ShouldContain(instruction);
        prompt.ShouldContain("Page content");
        prompt.ShouldContain("<data>");
        prompt.ShouldContain("Editor body.");
        prompt.ShouldContain("Home page");
        prompt.ShouldContain("fr-FR");
    }

    [Fact]
    public async Task CompleteAsync_WithUsage_SurfacesUsageAndRecordsIt()
    {
        var usage = new UsageDetails { InputTokenCount = 120, OutputTokenCount = 30 };
        RespondWith(new ChatResponse(new ChatMessage(ChatRole.Assistant, """{"title":"H"}"""))
        {
            FinishReason = ChatFinishReason.Stop,
            Usage = usage,
        });

        StructuredCompletionResult<SeoExtraction> result =
            await _sut.CompleteAsync<SeoExtraction>(Request(), TestContext.Current.CancellationToken);

        result.Usage.ShouldNotBeNull();
        result.Usage.InputTokenCount.ShouldBe(120);
        await _usageTracker.Received(1).RecordAsync(Arg.Any<AIUsageRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_UnknownWorkspace_ReturnsTransportFailure()
    {
        _workspaceProvider
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AIWorkspace?)null);

        StructuredCompletionResult<SeoExtraction> result =
            await _sut.CompleteAsync<SeoExtraction>(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(StructuredCompletionStatus.TransportFailure);
        result.ErrorMessage.ShouldNotBeNull();
    }

    [Fact]
    public async Task CompleteAsync_QuotaDenied_ReturnsTransportFailure_AndDoesNotCallModel()
    {
        _quotaGuard.CheckAsync(Arg.Any<CancellationToken>()).Returns(AIQuotaResult.Denied("limit reached"));

        StructuredCompletionResult<SeoExtraction> result =
            await _sut.CompleteAsync<SeoExtraction>(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(StructuredCompletionStatus.TransportFailure);
        await _chatClient.DidNotReceive().GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_NullRequest_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.CompleteAsync<SeoExtraction>(null!, TestContext.Current.CancellationToken));

    private sealed class StubMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);

        public void Dispose() { }
    }
}
