using System.Text.Json;
using Granit.AI;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Workflow.AI.Internal;

/// <summary>
/// LLM-based implementation of <see cref="IAIApprovalEvaluator"/> that uses
/// <see cref="IAIChatClientFactory"/> to evaluate transition risk.
/// </summary>
internal sealed partial class LlmApprovalEvaluator(
    IAIChatClientFactory chatClientFactory,
    IOptions<WorkflowAIOptions> options,
    ILogger<LlmApprovalEvaluator> logger) : IAIApprovalEvaluator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public async Task<RiskAssessment> EvaluateRiskAsync(
        string entityType,
        string transition,
        string entityContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(entityContext);

        WorkflowAIOptions workflowOptions = options.Value;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(workflowOptions.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(workflowOptions.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string prompt = BuildPrompt(entityType, transition, entityContext);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, prompt),
            };

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;
            responseText = StripMarkdownCodeFences(responseText);

            LlmRiskResponse? result = JsonSerializer.Deserialize<LlmRiskResponse>(responseText, SerializerOptions);

            if (result is null)
            {
                LogDeserializationFailed(entityType, transition);
                return new RiskAssessment(1.0, "Failed to parse risk assessment from LLM.", []);
            }

            double riskScore = Math.Clamp(result.RiskScore, 0.0, 1.0);
            IReadOnlyList<string> riskFactors = result.RiskFactors ?? [];

            LogEvaluationSucceeded(entityType, transition, riskScore, riskFactors.Count);

            return new RiskAssessment(
                riskScore,
                result.Reasoning ?? string.Empty,
                riskFactors);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogTimeout(entityType, transition, workflowOptions.TimeoutSeconds);
            return new RiskAssessment(1.0, $"Risk evaluation timed out after {workflowOptions.TimeoutSeconds} seconds.", []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            LogJsonError(entityType, transition, ex.Message);
            return new RiskAssessment(1.0, $"Failed to parse LLM response: {ex.Message}", []);
        }
        catch (Exception ex)
        {
            LogError(entityType, transition, ex.Message);
            return new RiskAssessment(1.0, $"Risk evaluation failed: {ex.Message}", []);
        }
    }

    private static string BuildPrompt(string entityType, string transition, string entityContext) =>
        $"""
         You are a risk evaluator for workflow transitions. Evaluate the risk of performing
         the following transition.

         Entity type: {entityType}
         Transition: {transition}

         Entity context:
         ---
         {entityContext}
         ---

         Evaluate the risk factors and provide a risk assessment. Consider:
         - Data completeness and consistency
         - Compliance implications
         - Business rule violations
         - Potential for data loss or irreversible changes

         Respond with a JSON object containing:
         - "riskScore": a number between 0.0 (no risk) and 1.0 (highest risk)
         - "reasoning": a brief explanation of the overall risk assessment
         - "riskFactors": an array of strings, each describing a specific risk factor

         Return ONLY valid JSON, no markdown, no explanation.
         """;

    private static string StripMarkdownCodeFences(string text)
    {
        ReadOnlySpan<char> span = text.AsSpan().Trim();

        if (span.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            span = span["```json".Length..];
        }
        else if (span.StartsWith("```", StringComparison.Ordinal))
        {
            span = span["```".Length..];
        }

        if (span.EndsWith("```", StringComparison.Ordinal))
        {
            span = span[..^"```".Length];
        }

        return span.Trim().ToString();
    }

    private sealed record LlmRiskResponse(
        double RiskScore,
        string? Reasoning,
        IReadOnlyList<string>? RiskFactors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Risk evaluation succeeded for {EntityType} transition {Transition}: score {RiskScore:F2} with {RiskFactorCount} risk factors")]
    private partial void LogEvaluationSucceeded(string entityType, string transition, double riskScore, int riskFactorCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize LLM risk response for {EntityType} transition {Transition}")]
    private partial void LogDeserializationFailed(string entityType, string transition);

    [LoggerMessage(Level = LogLevel.Error, Message = "Risk evaluation timed out for {EntityType} transition {Transition} after {TimeoutSeconds}s")]
    private partial void LogTimeout(string entityType, string transition, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse LLM risk JSON for {EntityType} transition {Transition}: {ErrorMessage}")]
    private partial void LogJsonError(string entityType, string transition, string errorMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "Risk evaluation failed for {EntityType} transition {Transition}: {ErrorMessage}")]
    private partial void LogError(string entityType, string transition, string errorMessage);
}
