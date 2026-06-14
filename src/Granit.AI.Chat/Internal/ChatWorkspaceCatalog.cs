using Granit.AI.Chat.Settings;
using Granit.AI.Workspaces;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// Default <see cref="IChatWorkspaceCatalog"/>. Filters the tenant's workspaces to the
/// chat-capable ones via <see cref="IAIWorkspaceCapabilityResolver"/> and prepends the reserved
/// <c>Auto</c> option.
/// </summary>
internal sealed class ChatWorkspaceCatalog(
    IAIWorkspaceProvider workspaceProvider,
    IAIWorkspaceCapabilityResolver capabilityResolver) : IChatWorkspaceCatalog
{
    public async ValueTask<IReadOnlyList<string>> GetSelectableWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AIWorkspace> workspaces = await workspaceProvider.GetAllAsync(cancellationToken).ConfigureAwait(false);

        List<string> selectable = [AIChatSettingNames.ReservedAutoWorkspace];
        foreach (AIWorkspace workspace in workspaces)
        {
            AIModelCapabilities? capabilities = await capabilityResolver
                .ResolveAsync(workspace.Provider, workspace.Model, cancellationToken)
                .ConfigureAwait(false);

            // Include unless the catalog reports the model as explicitly non-chat — an unknown
            // capability set (null) is treated as chat-capable, mirroring the send-time guard.
            if (capabilities is not { Chat: false })
            {
                selectable.Add(workspace.Name);
            }
        }

        return selectable;
    }
}
