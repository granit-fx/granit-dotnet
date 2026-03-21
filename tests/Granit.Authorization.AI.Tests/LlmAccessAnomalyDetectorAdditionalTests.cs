using Granit.AI;
using Granit.Authorization.AI.Internal;
using Granit.Authorization.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.Authorization.AI.Tests;

public sealed class LlmAccessAnomalyDetectorAdditionalTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<AuthorizationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new AuthorizationAIOptions { WorkspaceName = "default", TimeoutSeconds = 5 });

    private LlmAccessAnomalyDetector CreateDetector(ILogger<LlmAccessAnomalyDetector>? logger = null) =>
        new(_chatClientFactory, _options, logger ?? NullLogger<LlmAccessAnomalyDetector>.Instance);

    // ── ParseResponse edge cases ───────────────────────────────────────────

    [Fact]
    public void ParseResponse_NegativeScore_ClampedToZero()
    {
        const string response = """{ "score": -0.5, "reasoning": "Under min", "riskFactors": [] }""";

        AccessRiskScore result = LlmAccessAnomalyDetector.ParseResponse(response);

        result.Score.ShouldBe(0.0);
    }

    [Fact]
    public void ParseResponse_MissingReasoning_DefaultsToNoReasoningProvided()
    {
        const string response = """{ "score": 0.3, "reasoning": null, "riskFactors": [] }""";

        AccessRiskScore result = LlmAccessAnomalyDetector.ParseResponse(response);

        result.Reasoning.ShouldBe("No reasoning provided");
    }

    [Fact]
    public void ParseResponse_MissingRiskFactors_DefaultsToEmptyList()
    {
        const string response = """{ "score": 0.3, "reasoning": "Low risk", "riskFactors": null }""";

        AccessRiskScore result = LlmAccessAnomalyDetector.ParseResponse(response);

        result.RiskFactors.ShouldBeEmpty();
    }

    [Fact]
    public void ParseResponse_InvalidJson_ThrowsJsonException()
    {
        const string response = "this is not valid json";

        // ParseResponse is an internal method — invalid JSON propagates as JsonException.
        // The caller (EvaluateAccessAsync) catches it via the generic catch block.
        Should.Throw<System.Text.Json.JsonException>(
            () => LlmAccessAnomalyDetector.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_EmptyString_ThrowsJsonException()
    {
        // Empty string is not valid JSON — JsonSerializer throws.
        Should.Throw<System.Text.Json.JsonException>(
            () => LlmAccessAnomalyDetector.ParseResponse(""));
    }

    [Fact]
    public void ParseResponse_MarkdownFencesWithoutLanguage_ParsesCorrectly()
    {
        const string response = """
            ```
            { "score": 0.4, "reasoning": "Moderate", "riskFactors": ["factor1"] }
            ```
            """;

        AccessRiskScore result = LlmAccessAnomalyDetector.ParseResponse(response);

        result.Score.ShouldBe(0.4);
        result.Reasoning.ShouldBe("Moderate");
    }

    [Fact]
    public void ParseResponse_ExactlyZeroScore_IsValid()
    {
        const string response = """{ "score": 0.0, "reasoning": "No risk", "riskFactors": [] }""";

        AccessRiskScore result = LlmAccessAnomalyDetector.ParseResponse(response);

        result.Score.ShouldBe(0.0);
    }

    [Fact]
    public void ParseResponse_ExactlyOneScore_IsValid()
    {
        const string response = """{ "score": 1.0, "reasoning": "Critical risk", "riskFactors": ["critical"] }""";

        AccessRiskScore result = LlmAccessAnomalyDetector.ParseResponse(response);

        result.Score.ShouldBe(1.0);
    }

    // ── Timeout scenario ───────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAccessAsync_Timeout_ReturnsFailOpenScore()
    {
        // Simulate a timeout by throwing OperationCanceledException from a non-user cancellation source
        _chatClientFactory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        LlmAccessAnomalyDetector detector = CreateDetector();

        AccessRiskScore result = await detector.EvaluateAccessAsync(
            "user-timeout", "Admin.Access", cancellationToken: TestContext.Current.CancellationToken);

        result.Score.ShouldBe(0.0);
        result.Reasoning.ShouldContain("fail-open");
    }

    [Fact]
    public async Task EvaluateAccessAsync_UserCancellation_Throws()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // When the user's token is cancelled, the linked CTS propagates it.
        // The code re-throws OperationCanceledException when cancellationToken.IsCancellationRequested.
        _chatClientFactory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        LlmAccessAnomalyDetector detector = CreateDetector();

        await Should.ThrowAsync<OperationCanceledException>(
            () => detector.EvaluateAccessAsync("user-1", "Documents.Read", cancellationToken: cts.Token));
    }

    // ── BuildPrompt additional ─────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_ContainsScoreGuidelines()
    {
        string prompt = LlmAccessAnomalyDetector.BuildPrompt("user-1", "Admin.Access", null);

        prompt.ShouldContain("0.0-0.3");
        prompt.ShouldContain("0.3-0.7");
        prompt.ShouldContain("0.7-1.0");
    }

    [Fact]
    public void BuildPrompt_ContainsJsonStructure()
    {
        string prompt = LlmAccessAnomalyDetector.BuildPrompt("user-1", "Admin.Access", null);

        prompt.ShouldContain("score");
        prompt.ShouldContain("reasoning");
        prompt.ShouldContain("riskFactors");
    }
}
