namespace Granit.AI.Chat.Endpoints.Dtos;

/// <summary>
/// The workspaces a user may select as their default chat workspace: the reserved <c>Auto</c>
/// option plus the chat-capable workspace names. Backs the settings UI dropdown.
/// </summary>
/// <param name="Workspaces">The selectable workspace names (<c>Auto</c> first).</param>
public sealed record ChatWorkspacesResponse(IReadOnlyList<string> Workspaces);
