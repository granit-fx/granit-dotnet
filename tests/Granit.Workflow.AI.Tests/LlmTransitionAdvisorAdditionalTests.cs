using Granit.AI;
using Granit.Workflow.AI.Internal;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.Workflow.AI.Tests;

/// <summary>
/// Additional tests for <see cref="LlmTransitionAdvisor"/> covering null parameter checks
/// and edge cases not covered by the main test class.
/// </summary>
public sealed class LlmTransitionAdvisorAdditionalTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<WorkflowAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new WorkflowAIOptions
    {
        WorkspaceName = "test",
        TimeoutSeconds = 10,
    });

    private readonly LlmTransitionAdvisor _sut;

    public LlmTransitionAdvisorAdditionalTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _sut = new LlmTransitionAdvisor(
            _chatClientFactory,
            _options,
            NullLogger<LlmTransitionAdvisor>.Instance);
    }

    [Fact]
    public async Task RecommendAsync_NullCurrentState_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.RecommendAsync("Document", null!, "{}", ["Publish"], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecommendAsync_NullEntityContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.RecommendAsync("Document", "Draft", null!, ["Publish"], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecommendAsync_NullAllowedTransitions_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.RecommendAsync("Document", "Draft", "{}", null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecommendAsync_ConfidenceBelowZero_ClampedToZero()
    {
        // Arrange
        const string jsonResponse = """{"recommendedTransition":"Publish","reasoning":"Negative confidence.","confidence":-0.5}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        TransitionRecommendation? result = await _sut.RecommendAsync(
            "Document",
            "Draft",
            "{}",
            ["Publish"],
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Confidence.ShouldBe(0.0);
    }

    [Fact]
    public async Task RecommendAsync_NullRecommendedTransition_ReturnsNull()
    {
        // Arrange
        const string jsonResponse = """{"recommendedTransition":null,"reasoning":"Cannot decide.","confidence":0.1}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        TransitionRecommendation? result = await _sut.RecommendAsync(
            "Document",
            "Draft",
            "{}",
            ["Publish"],
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RecommendAsync_EmptyRecommendedTransition_ReturnsNull()
    {
        // Arrange
        const string jsonResponse = """{"recommendedTransition":"","reasoning":"Cannot decide.","confidence":0.1}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        TransitionRecommendation? result = await _sut.RecommendAsync(
            "Document",
            "Draft",
            "{}",
            ["Publish"],
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RecommendAsync_NullReasoningInResponse_ReturnsEmptyString()
    {
        // Arrange
        const string jsonResponse = """{"recommendedTransition":"Publish","reasoning":null,"confidence":0.8}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        TransitionRecommendation? result = await _sut.RecommendAsync(
            "Document",
            "Draft",
            "{}",
            ["Publish"],
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Reasoning.ShouldBe(string.Empty);
    }
}
