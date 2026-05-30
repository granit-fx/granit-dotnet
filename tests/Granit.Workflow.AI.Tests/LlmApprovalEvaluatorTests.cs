using Granit.AI;
using Granit.Workflow.AI.Internal;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using LlmRiskResponse = Granit.Workflow.AI.Internal.LlmApprovalEvaluator.LlmRiskResponse;

namespace Granit.Workflow.AI.Tests;

public sealed class LlmApprovalEvaluatorTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<WorkflowAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new WorkflowAIOptions { WorkspaceName = "test", TimeoutSeconds = 10, AutoApprovalThreshold = 0.3 });

    private LlmApprovalEvaluator CreateSut() => new(_structured, _options, NullLogger<LlmApprovalEvaluator>.Instance);

    private void Succeeds(LlmRiskResponse value) =>
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRiskResponse> { Status = StructuredCompletionStatus.Succeeded, Value = value });

    private void Fails(StructuredCompletionStatus status) =>
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRiskResponse> { Status = status });

    [Fact]
    public async Task EvaluateRiskAsync_ValidLowRiskResponse_ReturnsLowRiskScore()
    {
        Succeeds(new LlmRiskResponse(0.15, "Standard document publication.", ["Minor formatting inconsistency"]));

        RiskAssessment result = await CreateSut().EvaluateRiskAsync(
            "Document", "Publish", """{"status":"reviewed"}""", TestContext.Current.CancellationToken);

        result.RiskScore.ShouldBe(0.15);
        result.Reasoning.ShouldBe("Standard document publication.");
        result.RiskFactors.Count.ShouldBe(1);
        result.RiskFactors[0].ShouldBe("Minor formatting inconsistency");
    }

    [Fact]
    public async Task EvaluateRiskAsync_ValidHighRiskResponse_ReturnsHighRiskScore()
    {
        Succeeds(new LlmRiskResponse(0.85, "Financial data requires review.",
            ["Missing approval chain", "Amount exceeds threshold", "Compliance flag"]));

        RiskAssessment result = await CreateSut().EvaluateRiskAsync(
            "Invoice", "Approve", """{"amount":50000}""", TestContext.Current.CancellationToken);

        result.RiskScore.ShouldBe(0.85);
        result.RiskFactors.Count.ShouldBe(3);
    }

    [Fact]
    public async Task EvaluateRiskAsync_SchemaViolation_ReturnsMaxRisk()
    {
        Fails(StructuredCompletionStatus.SchemaViolation);

        RiskAssessment result = await CreateSut().EvaluateRiskAsync(
            "Document", "Publish", "{}", TestContext.Current.CancellationToken);

        result.RiskScore.ShouldBe(1.0);
        result.Reasoning.ShouldContain("maximum risk");
    }

    [Fact]
    public async Task EvaluateRiskAsync_TransportFailure_ReturnsMaxRisk()
    {
        Fails(StructuredCompletionStatus.TransportFailure);

        RiskAssessment result = await CreateSut().EvaluateRiskAsync(
            "Invoice", "Approve", "{}", TestContext.Current.CancellationToken);

        result.RiskScore.ShouldBe(1.0);
        result.Reasoning.ShouldContain("maximum risk");
    }

    [Fact]
    public async Task EvaluateRiskAsync_RiskScoreAboveOne_ClampedToOne()
    {
        Succeeds(new LlmRiskResponse(2.5, "Very risky.", []));

        RiskAssessment result = await CreateSut().EvaluateRiskAsync(
            "Document", "Delete", "{}", TestContext.Current.CancellationToken);

        result.RiskScore.ShouldBe(1.0);
    }

    [Fact]
    public async Task EvaluateRiskAsync_PassesContextToPrimitive()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRiskResponse>
            {
                Status = StructuredCompletionStatus.Succeeded,
                Value = new LlmRiskResponse(0.2, "ok", []),
            });

        await CreateSut().EvaluateRiskAsync("Invoice", "Approve", "the context", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.ShouldBe("the context");
        captured.Context.ShouldNotBeNull();
        captured.Context.ShouldContain(kv => kv.Key == "Entity type" && kv.Value == "Invoice");
        captured.Context.ShouldContain(kv => kv.Key == "Transition" && kv.Value == "Approve");
    }

    [Fact]
    public async Task EvaluateRiskAsync_NullEntityType_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            CreateSut().EvaluateRiskAsync(null!, "Publish", "{}", TestContext.Current.CancellationToken));
}
