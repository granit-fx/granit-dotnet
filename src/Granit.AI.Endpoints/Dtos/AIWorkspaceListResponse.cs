namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Response containing a list of AI workspaces.
/// </summary>
/// <param name="Workspaces">The workspace list.</param>
/// <param name="TotalCount">Total number of workspaces.</param>
public sealed record AIWorkspaceListResponse(
    IReadOnlyList<AIWorkspaceResponse> Workspaces,
    int TotalCount);
