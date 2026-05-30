using Granit.AI;
using Granit.Authorization.AI.Internal;
using Granit.Authorization.AI.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using LlmRiskResponse = Granit.Authorization.AI.Internal.LlmAccessAnomalyDetector.LlmRiskResponse;

namespace Granit.Authorization.AI.Tests;

public sealed class LlmAccessAnomalyDetectorAdditionalTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<AuthorizationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new AuthorizationAIOptions { WorkspaceName = "default", TimeoutSeconds = 5, UnavailableRiskScore = 0.5 });

    private LlmAccessAnomalyDetector CreateDetector() =>
        new(_structured, _options, NullLogger<LlmAccessAnomalyDetector>.Instance);

    private async Task<AccessRiskScore> Evaluate(LlmRiskResponse value)
    {
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRiskResponse> { Status = StructuredCompletionStatus.Succeeded, Value = value });
        return await CreateDetector().EvaluateAccessAsync(
            "user-1", "Admin.Access", cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EvaluateAccessAsync_NegativeScore_ClampedToZero()
    {
        AccessRiskScore result = await Evaluate(new LlmRiskResponse(-0.5, "Under min", []));

        result.Score.ShouldBe(0.0);
    }

    [Fact]
    public async Task EvaluateAccessAsync_ExactlyZeroScore_IsValid()
    {
        AccessRiskScore result = await Evaluate(new LlmRiskResponse(0.0, "No risk", []));

        result.Score.ShouldBe(0.0);
    }

    [Fact]
    public async Task EvaluateAccessAsync_ExactlyOneScore_IsValid()
    {
        AccessRiskScore result = await Evaluate(new LlmRiskResponse(1.0, "Critical risk", ["critical"]));

        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public async Task EvaluateAccessAsync_CallerCancellation_Throws()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Caller cancellation (not the aggressive timeout) must propagate, not fall back.
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<StructuredCompletionResult<LlmRiskResponse>>(_ => throw new OperationCanceledException(cts.Token));

        await Should.ThrowAsync<OperationCanceledException>(
            () => CreateDetector().EvaluateAccessAsync("user-1", "Documents.Read", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task EvaluateAccessAsync_InstructionCarriesScoreGuidelines()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRiskResponse>
            {
                Status = StructuredCompletionStatus.Succeeded,
                Value = new LlmRiskResponse(0.1, "ok", []),
            });

        await CreateDetector().EvaluateAccessAsync("user-1", "Admin.Access", cancellationToken: TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Instruction.ShouldNotBeNull();
        captured.Instruction.ShouldContain("0.0-0.3");
        captured.Instruction.ShouldContain("0.7-1.0");
    }
}
