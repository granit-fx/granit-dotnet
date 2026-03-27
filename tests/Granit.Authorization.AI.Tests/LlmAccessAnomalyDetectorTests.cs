using Granit.AI;
using Granit.AI.Internal;
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

public sealed class LlmAccessAnomalyDetectorTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<AuthorizationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new AuthorizationAIOptions { WorkspaceName = "default", TimeoutSeconds = 5 });

    private LlmAccessAnomalyDetector CreateDetector(ILogger<LlmAccessAnomalyDetector>? logger = null) =>
        new(_chatClientFactory, _options, logger ?? NullLogger<LlmAccessAnomalyDetector>.Instance);

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
    public async Task EvaluateAccessAsync_NormalAccess_ReturnsLowRisk()
    {
        SetupChatResponse("""{ "score": 0.1, "reasoning": "Normal access pattern", "riskFactors": [] }""");

        LlmAccessAnomalyDetector detector = CreateDetector();

        AccessRiskScore result = await detector.EvaluateAccessAsync(
            "user-123", "Documents.Read", cancellationToken: TestContext.Current.CancellationToken);

        result.Score.ShouldBe(0.1);
        result.Reasoning.ShouldBe("Normal access pattern");
        result.RiskFactors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAccessAsync_SuspiciousAccess_ReturnsHighRisk()
    {
        SetupChatResponse("""
            { "score": 0.85, "reasoning": "Unusual admin access at 3 AM", "riskFactors": ["off-hours access", "elevated permission request"] }
            """);

        LlmAccessAnomalyDetector detector = CreateDetector();

        AccessRiskScore result = await detector.EvaluateAccessAsync(
            "user-456", "Admin.FullControl", "access at 3 AM from new IP",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Score.ShouldBe(0.85);
        result.Reasoning.ShouldBe("Unusual admin access at 3 AM");
        result.RiskFactors.Count.ShouldBe(2);
        result.RiskFactors.ShouldContain("off-hours access");
        result.RiskFactors.ShouldContain("elevated permission request");
    }

    [Fact]
    public async Task EvaluateAccessAsync_LLMFailure_ReturnsUncertaintyScore()
    {
        _chatClientFactory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        LlmAccessAnomalyDetector detector = CreateDetector();

        AccessRiskScore result = await detector.EvaluateAccessAsync(
            "user-789", "Documents.Write", cancellationToken: TestContext.Current.CancellationToken);

        result.Score.ShouldBe(0.5);
        result.Reasoning.ShouldContain("unavailable");
        result.RiskFactors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAccessAsync_NullUserId_ThrowsArgumentNullException()
    {
        LlmAccessAnomalyDetector detector = CreateDetector();

        await Should.ThrowAsync<ArgumentNullException>(
            () => detector.EvaluateAccessAsync(null!, "Documents.Read",
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateAccessAsync_NullPermission_ThrowsArgumentNullException()
    {
        LlmAccessAnomalyDetector detector = CreateDetector();

        await Should.ThrowAsync<ArgumentNullException>(
            () => detector.EvaluateAccessAsync("user-123", null!,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ParseResponse_WithMarkdownFences_ParsesCorrectly()
    {
        const string response = """
            ```json
            { "score": 0.5, "reasoning": "Moderate risk", "riskFactors": ["unusual time"] }
            ```
            """;

        AccessRiskScore result = LlmAccessAnomalyDetector.ParseResponse(response);

        result.Score.ShouldBe(0.5);
        result.Reasoning.ShouldBe("Moderate risk");
        result.RiskFactors.Count.ShouldBe(1);
    }

    [Fact]
    public void ParseResponse_ScoreOutOfRange_ClampedToValidRange()
    {
        const string response = """{ "score": 1.5, "reasoning": "Over max", "riskFactors": [] }""";

        AccessRiskScore result = LlmAccessAnomalyDetector.ParseResponse(response);

        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public void ParseResponse_NullResponse_ReturnsZeroScore()
    {
        AccessRiskScore result = LlmAccessAnomalyDetector.ParseResponse("null");

        result.Score.ShouldBe(0.0);
        result.Reasoning.ShouldContain("Unable to parse");
    }

    [Fact]
    public void BuildPrompt_WithContext_IncludesContext()
    {
        string prompt = LlmAccessAnomalyDetector.BuildPrompt("user-1", "Admin.Access", "off-hours login");

        prompt.ShouldContain("off-hours login");
        prompt.ShouldContain(LlmInputSanitizer.PseudonymizeUserId("user-1"));
        prompt.ShouldContain("Admin.Access");
    }

    [Fact]
    public void BuildPrompt_WithoutContext_OmitsContextPart()
    {
        string prompt = LlmAccessAnomalyDetector.BuildPrompt("user-1", "Documents.Read", null);

        prompt.ShouldNotContain("Additional context:");
        prompt.ShouldContain(LlmInputSanitizer.PseudonymizeUserId("user-1"));
        prompt.ShouldContain("Documents.Read");
    }
}
