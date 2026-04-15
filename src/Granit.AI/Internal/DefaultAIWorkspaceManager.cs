using Granit.AI.Workspaces;

namespace Granit.AI.Internal;

/// <summary>
/// Default workspace manager that delegates to <see cref="IAIWorkspaceStoreWriter"/>
/// after validating that only <see cref="AIWorkspaceKind.Dynamic"/> workspaces are modified.
/// </summary>
internal sealed class DefaultAIWorkspaceManager(
    IAIWorkspaceStoreReader reader,
    IAIWorkspaceStoreWriter writer) : IAIWorkspaceManager
{
    public async Task CreateAsync(AIWorkspace workspace, CancellationToken cancellationToken = default)
    {
        EnsureDynamic(workspace.Kind);

        AIWorkspace? existing = await reader.FindAsync(workspace.Name, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Workspace '{workspace.Name}' already exists.");
        }

        await writer.CreateAsync(workspace, cancellationToken);
    }

    public async Task UpdateAsync(AIWorkspace workspace, CancellationToken cancellationToken = default)
    {
        AIWorkspace? existing = await reader.FindAsync(workspace.Name, cancellationToken);
        if (existing is null)
        {
            throw new InvalidOperationException($"Workspace '{workspace.Name}' not found.");
        }

        EnsureDynamic(existing.Kind);
        await writer.UpdateAsync(workspace, cancellationToken);
    }

    public async Task DeleteAsync(string workspaceName, CancellationToken cancellationToken = default)
    {
        AIWorkspace? existing = await reader.FindAsync(workspaceName, cancellationToken);
        if (existing is null)
        {
            return;
        }

        EnsureDynamic(existing.Kind);
        await writer.DeleteAsync(workspaceName, cancellationToken);
    }

    private static void EnsureDynamic(AIWorkspaceKind kind)
    {
        if (kind != AIWorkspaceKind.Dynamic)
        {
            throw new InvalidOperationException("Only dynamic workspaces can be created, updated, or deleted.");
        }
    }
}
