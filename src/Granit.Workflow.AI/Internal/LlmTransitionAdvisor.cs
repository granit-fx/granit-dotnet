using System.Text.Json;
using Granit.AI;
using Granit.AI.Internal;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Workflow.AI.Internal;

/// <summary>
/// LLM-based implementation of <see cref="IAITransitionAdvisor"/> that uses
/// <see cref="IAIChatClientFactory"/> to recommend workflow transitions.
/// </summary>
internal sealed partial class LlmTransitionAdvisor(
    IAIChatClientFactory chatClientFactory,
    IOptions<WorkflowAIOptions> options,
    ILogger<LlmTransitionAdvisor> logger) : IAITransitionAdvisor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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

        try
        {
            // CreateAsync builds a fresh client per call (no cache) — dispose
            // deterministically so the HttpMessageHandler doesn't linger until GC.
            using IChatClient chatClient = await chatClientFactory
                .CreateAsync(workflowOptions.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string prompt = BuildPrompt(entityType, currentState, entityContext, allowedTransitions);

            List<ChatMessage> messages = [new ChatMessage(ChatRole.User, prompt)];

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;
            responseText = LlmResponseHelper.StripMarkdownCodeFences(responseText);

            LlmRecommendationResponse? result = JsonSerializer.Deserialize<LlmRecommendationResponse>(responseText, SerializerOptions);

            if (result is null || string.IsNullOrWhiteSpace(result.RecommendedTransition))
            {
                LogDeserializationFailed(entityType);
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            // Type only — a JSON parse message embeds a fragment of the LLM response.
            LogJsonError(entityType, ex.GetType().Name);
            return null;
        }
        catch (Exception ex)
        {
            // Type only — providers can echo the prompt payload in 4xx messages.
            LogError(entityType, ex.GetType().Name);
            return null;
        }
    }

    private static string BuildPrompt(
        string entityType,
        string currentState,
        string entityContext,
        IReadOnlyList<string> allowedTransitions)
    {
        var pb = new PromptBuilder(maxInputLength: 10_000);

        pb.AppendInstruction("You are a workflow advisor. Recommend the best next workflow transition.");
        pb.AppendInstruction(string.Empty);
        pb.AppendUserData("Entity type", entityType);
        pb.AppendUserData("Current state", currentState);
        pb.AppendInstruction($"Allowed transitions: {string.Join(", ", allowedTransitions)}");
        pb.AppendInstruction(string.Empty);
        pb.AppendUserTextBlock("Entity context", entityContext);
        pb.AppendInstruction("""

            Respond with a JSON object containing:
            - "recommendedTransition": one of the allowed transitions listed above
            - "reasoning": a brief explanation of why this transition is recommended
            - "confidence": a number between 0.0 and 1.0 indicating your confidence

            Return ONLY valid JSON, no markdown, no explanation.
            """);

        return pb.Build();
    }

    private sealed record LlmRecommendationResponse(
        string? RecommendedTransition,
        string? Reasoning,
        double Confidence);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transition recommendation succeeded for {EntityType} in state {CurrentState}: {RecommendedTransition} (confidence: {Confidence:F2})")]
    private partial void LogRecommendationSucceeded(string entityType, string currentState, string recommendedTransition, double confidence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No allowed transitions for {EntityType} in state {CurrentState}")]
    private partial void LogNoAllowedTransitions(string entityType, string currentState);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM recommended transition '{RecommendedTransition}' is not in the allowed set for {EntityType}")]
    private partial void LogInvalidRecommendation(string entityType, string recommendedTransition);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize LLM recommendation response for {EntityType}")]
    private partial void LogDeserializationFailed(string entityType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Transition recommendation timed out for {EntityType} after {TimeoutSeconds}s")]
    private partial void LogTimeout(string entityType, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse LLM recommendation JSON for {EntityType} (exception type: {ExceptionType})")]
    private partial void LogJsonError(string entityType, string exceptionType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Transition recommendation failed for {EntityType} (exception type: {ExceptionType})")]
    private partial void LogError(string entityType, string exceptionType);
}
