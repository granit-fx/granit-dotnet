using System.Text.Json;
using Granit.AI;
using Granit.Authorization.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Authorization.AI.Internal;

/// <summary>
/// LLM-backed access anomaly detector that evaluates access patterns for suspicious behavior.
/// </summary>
/// <remarks>
/// Uses a fail-open design: when the LLM is unavailable or times out,
/// access is allowed and a warning is logged for manual review.
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

            IChatClient chatClient = await chatClientFactory
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
            return AllowWithWarning();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogEvaluationFailed(ex);
            return AllowWithWarning();
        }
    }

    internal static string BuildPrompt(string userId, string permission, string? context)
    {
        string contextPart = context is not null
            ? $" Additional context: {context}."
            : string.Empty;

        return $$"""
            Analyze the following access request for anomalies and suspicious behavior.{{contextPart}}
            User ID: {{userId}}
            Permission requested: {{permission}}

            Return ONLY a JSON object with this exact structure (no markdown, no explanation):
            { "score": 0.0-1.0, "reasoning": "brief explanation", "riskFactors": ["factor1", "factor2"] }

            Score guidelines:
            - 0.0-0.3: Normal access pattern
            - 0.3-0.7: Unusual but not necessarily malicious
            - 0.7-1.0: Suspicious, warrants investigation
            """;
    }

    internal static AccessRiskScore ParseResponse(string responseText)
    {
        // Strip markdown code fences if present.
        string json = responseText.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = json.IndexOf('\n');
            int lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
            {
                json = json[(firstNewline + 1)..lastFence].Trim();
            }
        }

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

    private static AccessRiskScore AllowWithWarning() =>
        new(0.0, "AI evaluation unavailable — access allowed (fail-open)", []);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI access anomaly evaluation timed out after {TimeoutSeconds}s — access allowed (fail-open), flagged for manual review")]
    private partial void LogEvaluationTimeout(int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI access anomaly evaluation failed — access allowed (fail-open), flagged for manual review")]
    private partial void LogEvaluationFailed(Exception exception);

    private sealed record LlmRiskResponse
    {
        public double Score { get; init; }
        public string? Reasoning { get; init; }
        public List<string>? RiskFactors { get; init; }
    }
}
