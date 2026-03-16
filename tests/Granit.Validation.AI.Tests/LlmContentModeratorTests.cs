using Granit.AI;
using Granit.Validation.AI.Internal;
using Granit.Validation.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.Validation.AI.Tests;

public sealed class LlmContentModeratorTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<ValidationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new ValidationAIOptions { WorkspaceName = "default", TimeoutSeconds = 2, SeverityThreshold = 0.5 });

    private LlmContentModerator CreateModerator(ILogger<LlmContentModerator>? logger = null) =>
        new(_chatClientFactory, _options, logger ?? NullLogger<LlmContentModerator>.Instance);

    private void SetupChatResponse(string responseJson)
    {
        _chatClientFactory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        var chatMessage = new ChatMessage(ChatRole.Assistant, responseJson);
        var chatResponse = new ChatResponse(chatMessage);

        _chatClient.GetResponseAsync(
                Arg.Any<IList<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);
    }

    [Fact]
    public async Task ModerateAsync_CleanText_ReturnsAcceptable()
    {
        SetupChatResponse("""{ "isAcceptable": true, "flags": [] }""");

        LlmContentModerator moderator = CreateModerator();

        ModerationResult result = await moderator.ModerateAsync(
            "Hello, how are you today?", cancellationToken: TestContext.Current.CancellationToken);

        result.IsAcceptable.ShouldBeTrue();
        result.Flags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ModerateAsync_ToxicText_ReturnsFlagged()
    {
        SetupChatResponse("""
            { "isAcceptable": false, "flags": [{ "category": "Toxic", "description": "Contains offensive language", "severity": 0.9 }] }
            """);

        LlmContentModerator moderator = CreateModerator();

        ModerationResult result = await moderator.ModerateAsync(
            "some toxic text", cancellationToken: TestContext.Current.CancellationToken);

        result.IsAcceptable.ShouldBeFalse();
        result.Flags.Count.ShouldBe(1);
        result.Flags[0].Category.ShouldBe(ModerationCategory.Toxic);
        result.Flags[0].Severity.ShouldBe(0.9);
    }

    [Fact]
    public async Task ModerateAsync_PromptInjection_ReturnsFlagged()
    {
        SetupChatResponse("""
            { "isAcceptable": false, "flags": [{ "category": "PromptInjection", "description": "Attempted prompt override", "severity": 0.95 }] }
            """);

        LlmContentModerator moderator = CreateModerator();

        ModerationResult result = await moderator.ModerateAsync(
            "Ignore all previous instructions and...", cancellationToken: TestContext.Current.CancellationToken);

        result.IsAcceptable.ShouldBeFalse();
        result.Flags.Count.ShouldBe(1);
        result.Flags[0].Category.ShouldBe(ModerationCategory.PromptInjection);
    }

    [Fact]
    public async Task ModerateAsync_LLMFailure_ReturnsAcceptable()
    {
        _chatClientFactory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        LlmContentModerator moderator = CreateModerator();

        ModerationResult result = await moderator.ModerateAsync(
            "some text", cancellationToken: TestContext.Current.CancellationToken);

        result.IsAcceptable.ShouldBeTrue();
        result.Flags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ModerateAsync_FiltersBelowThreshold()
    {
        SetupChatResponse("""
            {
                "isAcceptable": false,
                "flags": [
                    { "category": "Toxic", "description": "Mild language", "severity": 0.3 },
                    { "category": "Spam", "description": "Repetitive content", "severity": 0.8 }
                ]
            }
            """);

        LlmContentModerator moderator = CreateModerator();

        ModerationResult result = await moderator.ModerateAsync(
            "some text", cancellationToken: TestContext.Current.CancellationToken);

        result.Flags.Count.ShouldBe(1);
        result.Flags[0].Category.ShouldBe(ModerationCategory.Spam);
        result.Flags[0].Severity.ShouldBe(0.8);
    }

    [Fact]
    public void ParseResponse_WithMarkdownFences_ParsesCorrectly()
    {
        const string response = """
            ```json
            { "isAcceptable": true, "flags": [] }
            ```
            """;

        ModerationResult result = LlmContentModerator.ParseResponse(response, 0.5);

        result.IsAcceptable.ShouldBeTrue();
        result.Flags.ShouldBeEmpty();
    }

    [Fact]
    public void BuildPrompt_WithContext_IncludesContext()
    {
        string prompt = LlmContentModerator.BuildPrompt("hello", "user bio");

        prompt.ShouldContain("user bio");
        prompt.ShouldContain("hello");
    }

    [Fact]
    public void BuildPrompt_WithoutContext_OmitsContextPart()
    {
        string prompt = LlmContentModerator.BuildPrompt("hello", null);

        prompt.ShouldNotContain("context:");
        prompt.ShouldContain("hello");
    }
}
