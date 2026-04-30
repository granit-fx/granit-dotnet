namespace Granit.Workspaces.Internal;

/// <summary>
/// Default <see cref="IWorkspaceContributionContext"/> — accumulates per-target
/// contribution payloads in memory; the <see cref="WorkspaceTreeComposer"/>
/// later merges them into the canonical descriptors.
/// </summary>
internal sealed class WorkspaceContributionContext : IWorkspaceContributionContext
{
    /// <summary>Contributions keyed by target workspace name.</summary>
    public Dictionary<string, List<WorkspaceSectionDescriptor>> Contributions { get; } =
        new(StringComparer.Ordinal);

    public IWorkspaceContributionBuilder ForWorkspace(string workspaceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceName);
        return new Builder(this, workspaceName);
    }

    private sealed class Builder(WorkspaceContributionContext parent, string workspaceName)
        : IWorkspaceContributionBuilder
    {
        public IWorkspaceContributionBuilder Section(string key, Action<WorkspaceSectionBuilder> configure)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(configure);

            WorkspaceSectionBuilder sectionBuilder = new(key);
            configure(sectionBuilder);
            WorkspaceSectionDescriptor descriptor = sectionBuilder.Build();

            if (!parent.Contributions.TryGetValue(workspaceName, out List<WorkspaceSectionDescriptor>? sections))
            {
                sections = [];
                parent.Contributions[workspaceName] = sections;
            }
            sections.Add(descriptor);

            return this;
        }
    }
}
