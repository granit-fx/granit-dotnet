using Granit.AI;
using Granit.AI.Internal;
using Granit.Authorization.AI.Internal;
using Granit.Authorization.AI.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using LlmRiskResponse = Granit.Authorization.AI.Internal.LlmAccessAnomalyDetector.LlmRiskResponse;

namespace Granit.Authorization.AI.Tests;

public sealed class LlmAccessAnomalyDetectorTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<AuthorizationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new AuthorizationAIOptions { WorkspaceName = "default", TimeoutSeconds = 5, UnavailableRiskScore = 0.5 });

    private LlmAccessAnomalyDetector CreateDetector() =>
        new(_structured, _options, NullLogger<LlmAccessAnomalyDetector>.Instance);

    private void CompletionReturns(StructuredCompletionStatus status, LlmRiskResponse? value = null) =>
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRiskResponse> { Status = status, Value = value });

    private void Succeeds(LlmRiskResponse value) => CompletionReturns(StructuredCompletionStatus.Succeeded, value);

    [Fact]
    public async Task EvaluateAccessAsync_NormalAccess_ReturnsLowRisk()
    {
        Succeeds(new LlmRiskResponse(0.1, "Normal access pattern", []));

        AccessRiskScore result = await CreateDetector().EvaluateAccessAsync(
            "user-123", "Documents.Read", cancellationToken: TestContext.Current.CancellationToken);

        result.Score.ShouldBe(0.1);
        result.Reasoning.ShouldBe("Normal access pattern");
        result.RiskFactors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAccessAsync_SuspiciousAccess_ReturnsHighRisk()
    {
        Succeeds(new LlmRiskResponse(0.85, "Unusual admin access at 3 AM",
            ["off-hours access", "elevated permission request"]));

        AccessRiskScore result = await CreateDetector().EvaluateAccessAsync(
            "user-456", "Admin.FullControl", "access at 3 AM from new IP",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Score.ShouldBe(0.85);
        result.Reasoning.ShouldBe("Unusual admin access at 3 AM");
        result.RiskFactors.Count.ShouldBe(2);
        result.RiskFactors.ShouldContain("off-hours access");
    }

    [Fact]
    public async Task EvaluateAccessAsync_ScoreOutOfRange_ClampedToValidRange()
    {
        Succeeds(new LlmRiskResponse(1.5, "Over max", []));

        AccessRiskScore result = await CreateDetector().EvaluateAccessAsync(
            "user-1", "Documents.Read", cancellationToken: TestContext.Current.CancellationToken);

        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public async Task EvaluateAccessAsync_NullReasoningAndFactors_UsesDefaults()
    {
        Succeeds(new LlmRiskResponse(0.4, null, null));

        AccessRiskScore result = await CreateDetector().EvaluateAccessAsync(
            "user-1", "Documents.Read", cancellationToken: TestContext.Current.CancellationToken);

        result.Reasoning.ShouldBe("No reasoning provided");
        result.RiskFactors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAccessAsync_TransportFailure_ReturnsUncertaintyScore()
    {
        CompletionReturns(StructuredCompletionStatus.TransportFailure);

        AccessRiskScore result = await CreateDetector().EvaluateAccessAsync(
            "user-789", "Documents.Write", cancellationToken: TestContext.Current.CancellationToken);

        result.Score.ShouldBe(0.5);
        result.Reasoning.ShouldContain("unavailable");
        result.RiskFactors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAccessAsync_SchemaViolation_ReturnsUncertaintyScore()
    {
        CompletionReturns(StructuredCompletionStatus.SchemaViolation);

        AccessRiskScore result = await CreateDetector().EvaluateAccessAsync(
            "user-1", "Documents.Read", cancellationToken: TestContext.Current.CancellationToken);

        result.Score.ShouldBe(0.5);
        result.Reasoning.ShouldContain("unavailable");
    }

    [Fact]
    public async Task EvaluateAccessAsync_FailClosedConfig_ReturnsHighRiskOnUnavailable()
    {
        IOptions<AuthorizationAIOptions> failClosed = Microsoft.Extensions.Options.Options.Create(
            new AuthorizationAIOptions { WorkspaceName = "default", TimeoutSeconds = 5, UnavailableRiskScore = 1.0 });
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRiskResponse> { Status = StructuredCompletionStatus.TransportFailure });

        var detector = new LlmAccessAnomalyDetector(_structured, failClosed, NullLogger<LlmAccessAnomalyDetector>.Instance);

        AccessRiskScore result = await detector.EvaluateAccessAsync(
            "user-1", "Documents.Read", cancellationToken: TestContext.Current.CancellationToken);

        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public async Task EvaluateAccessAsync_PseudonymizesUserId_AndPassesPermissionAsContent()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRiskResponse>
            {
                Status = StructuredCompletionStatus.Succeeded,
                Value = new LlmRiskResponse(0.1, "ok", []),
            });

        await CreateDetector().EvaluateAccessAsync("user-1", "Admin.Access", "off-hours login", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.ShouldBe("Admin.Access");
        captured.Context.ShouldNotBeNull();
        // User id must be pseudonymized, never raw, before leaving the process.
        captured.Context.ShouldContain(kv => kv.Key == "User ID" && kv.Value == LlmInputSanitizer.PseudonymizeUserId("user-1"));
        captured.Context.ShouldNotContain(kv => kv.Value == "user-1");
        captured.Context.ShouldContain(kv => kv.Key == "Additional context" && kv.Value == "off-hours login");
    }

    [Fact]
    public async Task EvaluateAccessAsync_WithoutContext_OmitsAdditionalContext()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<LlmRiskResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRiskResponse>
            {
                Status = StructuredCompletionStatus.Succeeded,
                Value = new LlmRiskResponse(0.1, "ok", []),
            });

        await CreateDetector().EvaluateAccessAsync("user-1", "Documents.Read", cancellationToken: TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Context.ShouldNotBeNull();
        captured.Context.Count.ShouldBe(1);
        captured.Context.ShouldNotContain(kv => kv.Key == "Additional context");
    }

    [Fact]
    public async Task EvaluateAccessAsync_NullUserId_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateDetector().EvaluateAccessAsync(null!, "Documents.Read",
                cancellationToken: TestContext.Current.CancellationToken));

    [Fact]
    public async Task EvaluateAccessAsync_NullPermission_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateDetector().EvaluateAccessAsync("user-123", null!,
                cancellationToken: TestContext.Current.CancellationToken));
}
