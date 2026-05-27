using System.Text.Json;
using Granit.AI;
using Granit.AI.Internal;
using Granit.Authorization.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Authorization.AI.Internal;

/// <summary>
/// LLM-backed access anomaly detector that evaluates access patterns for suspicious behavior.
/// </summary>
/// <remarks>
/// When the LLM is unavailable or times out, returns a configurable uncertainty score
/// (default 0.5) and logs a warning for manual review. Configure
/// <see cref="AuthorizationAIOptions.UnavailableRiskScore"/> to control fail-open (0.0)
/// or fail-closed (1.0) behavior.
/// </remarks>
internal sealed partial class LlmAccessAnomalyDetector(
    IAIChatClientFactory chatClientFactory,
    IOptions<AuthorizationAIOptions> options,
    ILogger<LlmAccessAnomalyDetector> logger) : IAIAccessAnomalyDetector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<AccessRiskScore> EvaluateAccessAsync(
        string userId,
        string permission,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(permission);

        AuthorizationAIOptions config = options.Value;

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token, cancellationToken);

            // CreateAsync builds a fresh client per call (no cache) — dispose
            // deterministically so the HttpMessageHandler doesn't linger until GC.
            using IChatClient chatClient = await chatClientFactory
                .CreateAsync(config.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string prompt = BuildPrompt(userId, permission, context);

            ChatResponse response = await chatClient.GetResponseAsync(
                prompt, cancellationToken: linkedCts.Token).ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            return ParseResponse(responseText);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogEvaluationTimeout(config.TimeoutSeconds);
            return UnavailableScore(config);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogEvaluationFailed(ex);
            return UnavailableScore(config);
        }
    }

    internal static string BuildPrompt(string userId, string permission, string? context)
    {
        var pb = new PromptBuilder(maxInputLength: 2_000);

        pb.AppendInstruction("Analyze the following access request for anomalies and suspicious behavior.");
        pb.AppendInstruction(string.Empty);

        // Pseudonymize userId to prevent PII leakage to external AI services.
        pb.AppendUserData("User ID", LlmInputSanitizer.PseudonymizeUserId(userId));
        pb.AppendUserData("Permission requested", permission);

        if (context is not null)
        {
            // Control character stripping is now handled by PromptBuilder.SanitizeInput().
            pb.AppendUserData("Additional context", context);
        }

        pb.AppendInstruction(string.Empty);
        pb.AppendInstruction("""
            Return ONLY a JSON object with this exact structure (no markdown, no explanation):
            { "score": 0.0-1.0, "reasoning": "brief explanation", "riskFactors": ["factor1", "factor2"] }

            Score guidelines:
            - 0.0-0.3: Normal access pattern
            - 0.3-0.7: Unusual but not necessarily malicious
            - 0.7-1.0: Suspicious, warrants investigation
            """);

        return pb.Build();
    }

    internal static AccessRiskScore ParseResponse(string responseText)
    {
        string json = LlmResponseHelper.StripMarkdownCodeFences(responseText);

        LlmRiskResponse? parsed = JsonSerializer.Deserialize<LlmRiskResponse>(json, JsonOptions);

        if (parsed is null)
        {
            return new AccessRiskScore(0.0, "Unable to parse AI response", []);
        }

        double score = Math.Clamp(parsed.Score, 0.0, 1.0);
        string reasoning = parsed.Reasoning ?? "No reasoning provided";
        List<string> riskFactors = parsed.RiskFactors ?? [];

        return new AccessRiskScore(score, reasoning, riskFactors);
    }

    private static AccessRiskScore UnavailableScore(AuthorizationAIOptions config) =>
        new(Math.Clamp(config.UnavailableRiskScore, 0.0, 1.0),
            "AI evaluation unavailable — flagged for manual review", []);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI access anomaly evaluation timed out after {TimeoutSeconds}s — flagged for manual review")]
    private partial void LogEvaluationTimeout(int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI access anomaly evaluation failed — flagged for manual review")]
    private partial void LogEvaluationFailed(Exception exception);

    private sealed record LlmRiskResponse(double Score, string? Reasoning, List<string>? RiskFactors);
}
