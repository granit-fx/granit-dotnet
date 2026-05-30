using Granit.AI;
using Granit.Workflow.AI.Internal;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using LlmRiskResponse = Granit.Workflow.AI.Internal.LlmApprovalEvaluator.LlmRiskResponse;

namespace Granit.Workflow.AI.Tests;

/// <summary>
/// Additional tests for <see cref="LlmApprovalEvaluator"/> covering null parameter checks
/// and edge cases not covered by the main test class.
/// </summary>
public sealed class LlmApprovalEvaluatorAdditionalTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<WorkflowAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new WorkflowAIOptions { WorkspaceName = "test", TimeoutSeconds = 10 });

    private LlmApprovalEvaluator CreateSut() => new(_structured, _options, NullLogger<LlmApprovalEvaluator>.Instance);

    private async Task<RiskAssessment> Evaluate(LlmRiskResponse value)
    {
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRiskResponse> { Status = StructuredCompletionStatus.Succeeded, Value = value });
        return await CreateSut().EvaluateRiskAsync("Document", "Publish", "{}", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EvaluateRiskAsync_NullTransition_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            CreateSut().EvaluateRiskAsync("Document", null!, "{}", TestContext.Current.CancellationToken));

    [Fact]
    public async Task EvaluateRiskAsync_NullEntityContext_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            CreateSut().EvaluateRiskAsync("Document", "Publish", null!, TestContext.Current.CancellationToken));

    [Fact]
    public async Task EvaluateRiskAsync_NullRiskFactorsInResponse_ReturnsEmptyList()
    {
        RiskAssessment result = await Evaluate(new LlmRiskResponse(0.2, "Low risk.", null));

        result.RiskScore.ShouldBe(0.2);
        result.RiskFactors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateRiskAsync_RiskScoreBelowZero_ClampedToZero()
    {
        RiskAssessment result = await Evaluate(new LlmRiskResponse(-0.5, "Negative risk.", []));

        result.RiskScore.ShouldBe(0.0);
    }

    [Fact]
    public async Task EvaluateRiskAsync_NullReasoningInResponse_ReturnsEmptyString()
    {
        RiskAssessment result = await Evaluate(new LlmRiskResponse(0.1, null, []));

        result.Reasoning.ShouldBe(string.Empty);
    }
}
