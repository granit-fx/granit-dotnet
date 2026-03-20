namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Response from an AI chat completion.
/// </summary>
/// <param name="WorkspaceName">Workspace that processed the request.</param>
/// <param name="Model">Model used for generation.</param>
/// <param name="Content">Generated response content.</param>
/// <param name="Usage">Token usage details, if available.</param>
/// <param name="Duration">Time taken for the completion.</param>
public sealed record AIChatResponse(
    string WorkspaceName,
    string Model,
    string Content,
    AIChatUsageResponse? Usage,
    TimeSpan Duration);

/// <summary>
/// Token usage details for a chat completion.
/// </summary>
/// <param name="InputTokens">Number of input tokens consumed.</param>
/// <param name="OutputTokens">Number of output tokens generated.</param>
/// <param name="EstimatedCostUsd">Estimated cost in USD, if available.</param>
public sealed record AIChatUsageResponse(
    int InputTokens,
    int OutputTokens,
    decimal? EstimatedCostUsd);
