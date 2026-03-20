namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Response representing an AI workspace configuration.
/// </summary>
/// <param name="Name">Unique workspace name.</param>
/// <param name="Provider">Provider identifier (e.g. <c>OpenAI</c>, <c>AzureOpenAI</c>).</param>
/// <param name="Model">Model identifier (e.g. <c>gpt-4o</c>).</param>
/// <param name="SystemPrompt">Optional system prompt.</param>
/// <param name="Temperature">Sampling temperature (0.0–2.0).</param>
/// <param name="MaxOutputTokens">Maximum tokens to generate.</param>
/// <param name="Kind">Whether this workspace is <c>System</c> or <c>Dynamic</c>.</param>
/// <param name="IsActive">Whether this workspace is active.</param>
public sealed record AIWorkspaceResponse(
    string Name,
    string Provider,
    string Model,
    string? SystemPrompt,
    float? Temperature,
    int? MaxOutputTokens,
    string Kind,
    bool IsActive);
