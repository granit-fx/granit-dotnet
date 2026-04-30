using Microsoft.Extensions.Logging;

namespace Granit.Workspaces.Internal;

/// <summary>
/// Default <see cref="IWorkspaceRegistry"/> built once at boot from the DI
/// snapshot. Construction runs <see cref="WorkspaceTreeComposer.Compose"/> —
/// merging contributions, dropping empty shells, asserting depth ≤ 4.
/// </summary>
internal sealed class WorkspaceRegistry : IWorkspaceRegistry
{
    private readonly Dictionary<string, WorkspaceDescriptor> _byName;

    public WorkspaceRegistry(
        IEnumerable<IWorkspaceDescriptor> definitions,
        IEnumerable<IWorkspaceContributor> contributors,
        ILogger<WorkspaceRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(logger);

        All = WorkspaceTreeComposer.Compose(definitions, contributors, logger);
        _byName = All.ToDictionary(w => w.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<WorkspaceDescriptor> All { get; }

    public WorkspaceDescriptor? GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _byName.TryGetValue(name, out WorkspaceDescriptor? descriptor) ? descriptor : null;
    }
}
