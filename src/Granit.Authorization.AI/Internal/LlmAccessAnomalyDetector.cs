using Granit.AI;
using Granit.AI.Internal;
using Granit.Authorization.AI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Authorization.AI.Internal;

/// <summary>
/// LLM-backed access anomaly detector that evaluates access patterns for suspicious behavior
/// via the <see cref="IStructuredCompletion"/> primitive (ADR-064).
/// </summary>
/// <remarks>
/// When the LLM is unavailable, times out, or returns an unusable response, returns a
/// configurable uncertainty score (default 0.5) and logs a warning for manual review.
/// Configure <see cref="AuthorizationAIOptions.UnavailableRiskScore"/> to control fail-open
/// (0.0) or fail-closed (1.0) behavior. The user id is pseudonymized before it leaves the
/// process; the primitive owns schema enforcement, usage tracking, and PII-safe errors.
/// </remarks>
internal sealed partial class LlmAccessAnomalyDetector(
    IStructuredCompletion structuredCompletion,
    IOptions<AuthorizationAIOptions> options,
    ILogger<LlmAccessAnomalyDetector> logger) : IAIAccessAnomalyDetector
{
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

        // Aggressive timeout (authorization must not block). Layered over the primitive's own
        // timeout; when ours fires the primitive rethrows the cancellation and we fall back.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        IReadOnlyList<KeyValuePair<string, string?>> requestContext = context is null
            ? [new("User ID", LlmInputSanitizer.PseudonymizeUserId(userId))]
            : [new("User ID", LlmInputSanitizer.PseudonymizeUserId(userId)), new("Additional context", context)];

        var request = new StructuredCompletionRequest
        {
            Instruction = AnomalyInstruction,
            Content = permission,
            ContentLabel = "Permission requested",
            Context = requestContext,
            WorkspaceName = config.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<LlmRiskResponse> result = await structuredCompletion
                .CompleteAsync<LlmRiskResponse>(request, linkedCts.Token)
                .ConfigureAwait(false);

            if (result.Status == StructuredCompletionStatus.Succeeded)
            {
                return BuildScore(result.Value!);
            }

            // Transport failure, refusal, or unusable output — fall back to the configured
            // uncertainty score and flag for manual review.
            LogEvaluationFailed(result.Status.ToString());
            return UnavailableScore(config);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogEvaluationTimeout(config.TimeoutSeconds);
            return UnavailableScore(config);
        }
    }

    internal const string AnomalyInstruction =
        """
        Analyze the supplied access request for anomalies and suspicious behavior, and return a
        structured risk assessment: a score between 0.0 and 1.0, brief reasoning, and any risk
        factors.
        Score guidelines:
        - 0.0-0.3: Normal access pattern
        - 0.3-0.7: Unusual but not necessarily malicious
        - 0.7-1.0: Suspicious, warrants investigation
        """;

    private static AccessRiskScore BuildScore(LlmRiskResponse parsed) =>
        new(
            Math.Clamp(parsed.Score, 0.0, 1.0),
            parsed.Reasoning ?? "No reasoning provided",
            parsed.RiskFactors ?? []);

    private static AccessRiskScore UnavailableScore(AuthorizationAIOptions config) =>
        new(Math.Clamp(config.UnavailableRiskScore, 0.0, 1.0),
            "AI evaluation unavailable — flagged for manual review", []);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI access anomaly evaluation timed out after {TimeoutSeconds}s — flagged for manual review")]
    private partial void LogEvaluationTimeout(int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI access anomaly evaluation unavailable ({Status}) — flagged for manual review")]
    private partial void LogEvaluationFailed(string status);

    internal sealed record LlmRiskResponse(double Score, string? Reasoning, List<string>? RiskFactors);
}
