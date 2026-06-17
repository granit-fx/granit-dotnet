using Granit.AI.Chat.Settings;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.Options;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// Default <see cref="IChatWorkspaceCatalog"/>. Filters the tenant's workspaces to the
/// chat-capable ones via <see cref="IAIWorkspaceCapabilityResolver"/> and prepends the reserved
/// <c>Auto</c> option — but only when the host's default workspace is itself chat-capable, so a
/// user can never pick an option that would 404 at send time.
/// </summary>
internal sealed class ChatWorkspaceCatalog(
    IAIWorkspaceProvider workspaceProvider,
    IAIWorkspaceCapabilityResolver capabilityResolver,
    IOptions<GranitAIOptions> aiOptions) : IChatWorkspaceCatalog
{
    public async ValueTask<IReadOnlyList<string>> GetSelectableWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AIWorkspace> workspaces = await workspaceProvider.GetAllAsync(cancellationToken).ConfigureAwait(false);

        List<string> chatCapable = [];
        foreach (AIWorkspace workspace in workspaces)
        {
            AIModelCapabilities? capabilities = await capabilityResolver
                .ResolveAsync(workspace.Provider, workspace.Model, cancellationToken)
                .ConfigureAwait(false);

            // Include unless the catalog reports the model as explicitly non-chat — an unknown
            // capability set (null) is treated as chat-capable, mirroring the send-time guard.
            if (capabilities is not { Chat: false })
            {
                chatCapable.Add(workspace.Name);
            }
        }

        // 'Auto' delegates to the host's configured default workspace (see
        // ChatService.ResolveWorkspaceNameAsync). Offer it only when that default actually resolves
        // to a chat-capable workspace; otherwise selecting it would throw at send time. This also
        // suppresses 'Auto' for the framework's placeholder default ("default"), which no host seeds.
        string defaultWorkspace = aiOptions.Value.DefaultWorkspace;
        bool autoIsResolvable = !string.IsNullOrWhiteSpace(defaultWorkspace)
            && chatCapable.Contains(defaultWorkspace, StringComparer.OrdinalIgnoreCase);

        return autoIsResolvable
            ? [AIChatSettingNames.ReservedAutoWorkspace, .. chatCapable]
            : chatCapable;
    }
}
