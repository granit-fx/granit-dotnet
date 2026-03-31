namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Request to update an existing dynamic AI workspace.
/// </summary>
/// <param name="Provider">Provider identifier.</param>
/// <param name="Model">Model identifier.</param>
/// <param name="SystemPrompt">Optional system prompt.</param>
/// <param name="Temperature">Optional sampling temperature (0.0–2.0).</param>
/// <param name="MaxOutputTokens">Optional maximum output tokens.</param>
/// <param name="IsActive">Whether the workspace should be active.</param>
public sealed record AIWorkspaceUpdateRequest(
    string Provider,
    string Model,
    string? SystemPrompt,
    float? Temperature,
    int? MaxOutputTokens,
    bool IsActive) : IAIWorkspaceMutableFields;
