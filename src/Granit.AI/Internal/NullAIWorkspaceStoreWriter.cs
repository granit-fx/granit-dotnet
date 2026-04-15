using Granit.AI.Workspaces;

namespace Granit.AI.Internal;

/// <summary>
/// No-op workspace store writer used when no persistence adapter is registered.
/// </summary>
internal sealed class NullAIWorkspaceStoreWriter : IAIWorkspaceStoreWriter
{
    public Task CreateAsync(AIWorkspace workspace, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UpdateAsync(AIWorkspace workspace, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeleteAsync(string workspaceName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
