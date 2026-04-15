namespace Granit.AI.Workspaces;

/// <summary>
/// Persistence abstraction for writing dynamic AI workspaces.
/// </summary>
/// <remarks>
/// Implemented by <c>Granit.AI.EntityFrameworkCore</c>.
/// Default: <see cref="NullAIWorkspaceStoreWriter"/> (no-op).
/// </remarks>
public interface IAIWorkspaceStoreWriter
{
    /// <inheritdoc cref="IAIWorkspaceManager.CreateAsync"/>
    Task CreateAsync(AIWorkspace workspace, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IAIWorkspaceManager.UpdateAsync"/>
    Task UpdateAsync(AIWorkspace workspace, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IAIWorkspaceManager.DeleteAsync"/>
    Task DeleteAsync(string workspaceName, CancellationToken cancellationToken = default);
}
