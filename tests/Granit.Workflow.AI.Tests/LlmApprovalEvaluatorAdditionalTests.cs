using Granit.AI;
using Granit.Workflow.AI.Internal;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.AI.Tests;

/// <summary>
/// Additional tests for <see cref="LlmApprovalEvaluator"/> covering null parameter checks
/// and edge cases not covered by the main test class.
/// </summary>
public sealed class LlmApprovalEvaluatorAdditionalTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<WorkflowAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new WorkflowAIOptions
    {
        WorkspaceName = "test",
        TimeoutSeconds = 10,
    });

    private readonly LlmApprovalEvaluator _sut;

    public LlmApprovalEvaluatorAdditionalTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _sut = new LlmApprovalEvaluator(
            _chatClientFactory,
            _options,
            NullLogger<LlmApprovalEvaluator>.Instance);
    }

    [Fact]
    public async Task EvaluateRiskAsync_NullTransition_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.EvaluateRiskAsync("Document", null!, "{}", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateRiskAsync_NullEntityContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.EvaluateRiskAsync("Document", "Publish", null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateRiskAsync_NullRiskFactorsInResponse_ReturnsEmptyList()
    {
        // Arrange
        const string jsonResponse = """{"riskScore":0.2,"reasoning":"Low risk.","riskFactors":null}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        RiskAssessment result = await _sut.EvaluateRiskAsync(
            "Document",
            "Publish",
            "{}",
            TestContext.Current.CancellationToken);

        // Assert
        result.RiskScore.ShouldBe(0.2);
        result.RiskFactors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateRiskAsync_RiskScoreBelowZero_ClampedToZero()
    {
        // Arrange
        const string jsonResponse = """{"riskScore":-0.5,"reasoning":"Negative risk.","riskFactors":[]}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        RiskAssessment result = await _sut.EvaluateRiskAsync(
            "Document",
            "Publish",
            "{}",
            TestContext.Current.CancellationToken);

        // Assert
        result.RiskScore.ShouldBe(0.0);
    }

    [Fact]
    public async Task EvaluateRiskAsync_MarkdownCodeFences_StripsAndParses()
    {
        // Arrange
        const string jsonWithFences = """
            ```json
            {"riskScore":0.4,"reasoning":"Moderate risk.","riskFactors":["Data gap"]}
            ```
            """;

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonWithFences)));

        // Act
        RiskAssessment result = await _sut.EvaluateRiskAsync(
            "Document",
            "Publish",
            "{}",
            TestContext.Current.CancellationToken);

        // Assert
        result.RiskScore.ShouldBe(0.4);
        result.RiskFactors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateRiskAsync_NullReasoningInResponse_ReturnsEmptyString()
    {
        // Arrange
        const string jsonResponse = """{"riskScore":0.1,"reasoning":null,"riskFactors":[]}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        RiskAssessment result = await _sut.EvaluateRiskAsync(
            "Document",
            "Publish",
            "{}",
            TestContext.Current.CancellationToken);

        // Assert
        result.Reasoning.ShouldBe(string.Empty);
    }
}
