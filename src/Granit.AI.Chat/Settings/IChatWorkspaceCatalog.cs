namespace Granit.AI.Chat.Settings;

/// <summary>
/// Lists the workspaces a user may pick as their default chat workspace (ADR-067): the
/// chat-capable workspaces visible to the current tenant, plus the reserved
/// <see cref="AIChatSettingNames.ReservedAutoWorkspace"/> option. Backs the settings UI dropdown.
/// </summary>
public interface IChatWorkspaceCatalog
{
    /// <summary>
    /// Returns the selectable default-workspace options: <c>Auto</c> first, then every
    /// chat-capable workspace name. A workspace with an unknown capability set is treated as
    /// chat-capable (the catalog simply has no entry for the model).
    /// </summary>
    ValueTask<IReadOnlyList<string>> GetSelectableWorkspacesAsync(CancellationToken cancellationToken = default);
}
