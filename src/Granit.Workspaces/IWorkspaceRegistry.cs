namespace Granit.Workspaces;

/// <summary>
/// Read-side registry that exposes every composed <see cref="WorkspaceDescriptor"/>
/// — built once at boot from the DI snapshot of <see cref="WorkspaceDefinition"/>s
/// merged with <see cref="IWorkspaceContributor"/> grafts.
/// </summary>
public interface IWorkspaceRegistry
{
    /// <summary>All composed workspaces, sorted by <c>Order</c> then <c>Name</c>.</summary>
    IReadOnlyList<WorkspaceDescriptor> All { get; }

    /// <summary>Get the descriptor by its wire identifier, or <see langword="null"/> when unknown.</summary>
    WorkspaceDescriptor? GetByName(string name);
}
