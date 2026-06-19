namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Request to update an existing dynamic AI workspace.
/// </summary>
/// <param name="Provider">Provider identifier.</param>
/// <param name="Model">Model identifier.</param>
/// <param name="WorkspaceModelName">
/// Optional human-readable display label (e.g. <c>GPT-4o</c>, <c>Support AI</c>).
/// Pass <see langword="null"/> to clear the label and fall back to the model identifier.
/// </param>
/// <param name="SystemPrompt">Optional system prompt.</param>
/// <param name="Temperature">Optional sampling temperature (0.0–2.0).</param>
/// <param name="MaxOutputTokens">Optional maximum output tokens.</param>
/// <param name="Activated">Whether the workspace should be active. Defaults to <c>false</c> when omitted.</param>
public sealed record AIWorkspaceUpdateRequest(
    string Provider,
    string Model,
    string? WorkspaceModelName = null,
    string? SystemPrompt = null,
    float? Temperature = null,
    int? MaxOutputTokens = null,
    bool Activated = false) : IAIWorkspaceMutableFields;
