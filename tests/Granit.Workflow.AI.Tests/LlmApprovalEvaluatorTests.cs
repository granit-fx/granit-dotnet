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

public sealed class LlmApprovalEvaluatorTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<WorkflowAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new WorkflowAIOptions
    {
        WorkspaceName = "test",
        TimeoutSeconds = 10,
        AutoApprovalThreshold = 0.3,
    });

    private readonly LlmApprovalEvaluator _sut;

    public LlmApprovalEvaluatorTests()
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
    public async Task EvaluateRiskAsync_ValidLowRiskResponse_ReturnsLowRiskScore()
    {
        // Arrange
        const string jsonResponse = """{"riskScore":0.15,"reasoning":"Standard document publication.","riskFactors":["Minor formatting inconsistency"]}""";

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
            """{"title":"Quarterly Report","status":"reviewed"}""",
            TestContext.Current.CancellationToken);

        // Assert
        result.RiskScore.ShouldBe(0.15);
        result.Reasoning.ShouldBe("Standard document publication.");
        result.RiskFactors.Count.ShouldBe(1);
        result.RiskFactors[0].ShouldBe("Minor formatting inconsistency");
    }

    [Fact]
    public async Task EvaluateRiskAsync_ValidHighRiskResponse_ReturnsHighRiskScore()
    {
        // Arrange
        const string jsonResponse = """{"riskScore":0.85,"reasoning":"Financial data requires review.","riskFactors":["Missing approval chain","Amount exceeds threshold","Compliance flag"]}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        RiskAssessment result = await _sut.EvaluateRiskAsync(
            "Invoice",
            "Approve",
            """{"amount":50000,"currency":"EUR"}""",
            TestContext.Current.CancellationToken);

        // Assert
        result.RiskScore.ShouldBe(0.85);
        result.RiskFactors.Count.ShouldBe(3);
    }

    [Fact]
    public async Task EvaluateRiskAsync_InvalidJson_ReturnsMaxRisk()
    {
        // Arrange
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "not valid json")));

        // Act
        RiskAssessment result = await _sut.EvaluateRiskAsync(
            "Document",
            "Publish",
            "{}",
            TestContext.Current.CancellationToken);

        // Assert
        result.RiskScore.ShouldBe(1.0);
        result.Reasoning.ShouldContain("parse");
    }

    [Fact]
    public async Task EvaluateRiskAsync_LlmFailure_ReturnsMaxRisk()
    {
        // Arrange
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Provider unavailable"));

        // Act
        RiskAssessment result = await _sut.EvaluateRiskAsync(
            "Invoice",
            "Approve",
            "{}",
            TestContext.Current.CancellationToken);

        // Assert
        result.RiskScore.ShouldBe(1.0);
        result.Reasoning.ShouldContain("failed");
    }

    [Fact]
    public async Task EvaluateRiskAsync_RiskScoreAboveOne_ClampedToOne()
    {
        // Arrange
        const string jsonResponse = """{"riskScore":2.5,"reasoning":"Very risky.","riskFactors":[]}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        RiskAssessment result = await _sut.EvaluateRiskAsync(
            "Document",
            "Delete",
            "{}",
            TestContext.Current.CancellationToken);

        // Assert
        result.RiskScore.ShouldBe(1.0);
    }

    [Fact]
    public async Task EvaluateRiskAsync_NullEntityType_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.EvaluateRiskAsync(null!, "Publish", "{}", TestContext.Current.CancellationToken));
    }
}
