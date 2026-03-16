using Granit.AI;
using Granit.Workflow.AI.Internal;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.Workflow.AI.Tests;

public sealed class LlmTransitionAdvisorTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<WorkflowAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new WorkflowAIOptions
    {
        WorkspaceName = "test",
        TimeoutSeconds = 10,
    });

    private readonly LlmTransitionAdvisor _sut;

    public LlmTransitionAdvisorTests()
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
    public async Task RecommendAsync_ValidResponse_ReturnsRecommendation()
    {
        // Arrange
        const string jsonResponse = """{"recommendedTransition":"Publish","reasoning":"Document is complete.","confidence":0.92}""";

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
            """{"title":"Report","completeness":100}""",
            ["Publish", "Archive"],
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.RecommendedTransition.ShouldBe("Publish");
        result.Reasoning.ShouldBe("Document is complete.");
        result.Confidence.ShouldBe(0.92);
    }

    [Fact]
    public async Task RecommendAsync_EmptyAllowedTransitions_ReturnsNull()
    {
        // Act
        TransitionRecommendation? result = await _sut.RecommendAsync(
            "Document",
            "Archived",
            "{}",
            [],
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RecommendAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "not valid json")));

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
    public async Task RecommendAsync_LlmFailure_ReturnsNull()
    {
        // Arrange
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Provider unavailable"));

        // Act
        TransitionRecommendation? result = await _sut.RecommendAsync(
            "Invoice",
            "Pending",
            "{}",
            ["Approve", "Reject"],
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RecommendAsync_ConfidenceAboveOne_ClampedToOne()
    {
        // Arrange
        const string jsonResponse = """{"recommendedTransition":"Approve","reasoning":"Looks good.","confidence":1.5}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        TransitionRecommendation? result = await _sut.RecommendAsync(
            "Invoice",
            "Pending",
            "{}",
            ["Approve"],
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Confidence.ShouldBe(1.0);
    }

    [Fact]
    public async Task RecommendAsync_MarkdownCodeFences_StripsAndParses()
    {
        // Arrange
        const string jsonWithFences = """
            ```json
            {"recommendedTransition":"Submit","reasoning":"Ready for review.","confidence":0.8}
            ```
            """;

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonWithFences)));

        // Act
        TransitionRecommendation? result = await _sut.RecommendAsync(
            "Document",
            "Draft",
            "{}",
            ["Submit"],
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.RecommendedTransition.ShouldBe("Submit");
    }

    [Fact]
    public async Task RecommendAsync_NullEntityType_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.RecommendAsync(null!, "Draft", "{}", ["Publish"], TestContext.Current.CancellationToken));
    }
}
