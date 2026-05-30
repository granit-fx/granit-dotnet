using Granit.AI;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Workflow.AI.Internal;

/// <summary>
/// LLM-based implementation of <see cref="IAITransitionAdvisor"/> built on the
/// <see cref="IStructuredCompletion"/> primitive (ADR-064). Recommends the best next
/// workflow transition, validating the recommendation against the allowed set.
/// </summary>
internal sealed partial class LlmTransitionAdvisor(
    IStructuredCompletion structuredCompletion,
    IOptions<WorkflowAIOptions> options,
    ILogger<LlmTransitionAdvisor> logger) : IAITransitionAdvisor
{
    /// <inheritdoc />
    public async Task<TransitionRecommendation?> RecommendAsync(
        string entityType,
        string currentState,
        string entityContext,
        IReadOnlyList<string> allowedTransitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(entityContext);
        ArgumentNullException.ThrowIfNull(allowedTransitions);

        if (allowedTransitions.Count == 0)
        {
            LogNoAllowedTransitions(entityType, currentState);
            return null;
        }

        WorkflowAIOptions workflowOptions = options.Value;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(workflowOptions.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var request = new StructuredCompletionRequest
        {
            Instruction = $"""
                You are a workflow advisor. Recommend the best next workflow transition.
                Allowed transitions: {string.Join(", ", allowedTransitions)}.
                Respond with recommendedTransition (one of the allowed transitions above), a brief
                reasoning, and a confidence between 0.0 and 1.0.
                """,
            Content = entityContext,
            ContentLabel = "Entity context",
            Context = [new("Entity type", entityType), new("Current state", currentState)],
            WorkspaceName = workflowOptions.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<LlmRecommendationResponse> response = await structuredCompletion
                .CompleteAsync<LlmRecommendationResponse>(request, linkedCts.Token)
                .ConfigureAwait(false);

            if (response.Status != StructuredCompletionStatus.Succeeded)
            {
                LogRecommendationUnavailable(entityType, response.Status.ToString());
                return null;
            }

            LlmRecommendationResponse result = response.Value!;

            if (string.IsNullOrWhiteSpace(result.RecommendedTransition))
            {
                LogRecommendationUnavailable(entityType, "EmptyRecommendation");
                return null;
            }

            if (!allowedTransitions.Contains(result.RecommendedTransition, StringComparer.OrdinalIgnoreCase))
            {
                LogInvalidRecommendation(entityType, result.RecommendedTransition);
                return null;
            }

            double confidence = Math.Clamp(result.Confidence, 0.0, 1.0);
            LogRecommendationSucceeded(entityType, currentState, result.RecommendedTransition, confidence);

            return new TransitionRecommendation(
                result.RecommendedTransition,
                result.Reasoning ?? string.Empty,
                confidence);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogTimeout(entityType, workflowOptions.TimeoutSeconds);
            return null;
        }
    }

    internal sealed record LlmRecommendationResponse(
        string? RecommendedTransition,
        string? Reasoning,
        double Confidence);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transition recommendation succeeded for {EntityType} in state {CurrentState}: {RecommendedTransition} (confidence: {Confidence:F2})")]
    private partial void LogRecommendationSucceeded(string entityType, string currentState, string recommendedTransition, double confidence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No allowed transitions for {EntityType} in state {CurrentState}")]
    private partial void LogNoAllowedTransitions(string entityType, string currentState);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM recommended transition '{RecommendedTransition}' is not in the allowed set for {EntityType}")]
    private partial void LogInvalidRecommendation(string entityType, string recommendedTransition);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transition recommendation unavailable for {EntityType} ({Status})")]
    private partial void LogRecommendationUnavailable(string entityType, string status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Transition recommendation timed out for {EntityType} after {TimeoutSeconds}s")]
    private partial void LogTimeout(string entityType, int timeoutSeconds);
}
