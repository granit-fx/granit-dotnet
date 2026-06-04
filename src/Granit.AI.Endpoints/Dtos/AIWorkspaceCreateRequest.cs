namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Request to create a new dynamic AI workspace.
/// </summary>
/// <param name="Name">Unique workspace name (lowercase alphanumeric with hyphens).</param>
/// <param name="Provider">Provider identifier (e.g. <c>OpenAI</c>, <c>AzureOpenAI</c>).</param>
/// <param name="Model">Model identifier (e.g. <c>gpt-4o</c>).</param>
/// <param name="SystemPrompt">Optional system prompt.</param>
/// <param name="Temperature">Optional sampling temperature (0.0–2.0).</param>
/// <param name="MaxOutputTokens">Optional maximum output tokens.</param>
public sealed record AIWorkspaceCreateRequest(
    string Name,
    string Provider,
    string Model,
    string? SystemPrompt = null,
    float? Temperature = null,
    int? MaxOutputTokens = null) : IAIWorkspaceMutableFields;
