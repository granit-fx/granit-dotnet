namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Request to update an existing dynamic AI workspace.
/// </summary>
/// <param name="Provider">Provider identifier.</param>
/// <param name="Model">Model identifier.</param>
/// <param name="SystemPrompt">Optional system prompt.</param>
/// <param name="Temperature">Optional sampling temperature (0.0–2.0).</param>
/// <param name="MaxOutputTokens">Optional maximum output tokens.</param>
/// <param name="Activated">Whether the workspace should be active. Defaults to <c>false</c> when omitted.</param>
public sealed record AIWorkspaceUpdateRequest(
    string Provider,
    string Model,
    string? SystemPrompt = null,
    float? Temperature = null,
    int? MaxOutputTokens = null,
    bool Activated = false) : IAIWorkspaceMutableFields;
