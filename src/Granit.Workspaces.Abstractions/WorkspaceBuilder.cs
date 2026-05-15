namespace Granit.Workspaces;

/// <summary>
/// Fluent builder for one workspace's UI surface. Used inside
/// <see cref="WorkspaceDefinition.Configure"/>.
/// </summary>
public sealed class WorkspaceBuilder
{
    private string? _displayKey;
    private string? _icon;
    private int _order;
    private string? _requiresPermission;
    private bool _isShell;
    private readonly List<Func<WorkspaceSectionDescriptor>> _sectionFactories = [];

    /// <summary>Sets the i18n key for the workspace's display name.</summary>
    public WorkspaceBuilder DisplayKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _displayKey = key;
        return this;
    }

    /// <summary>Sets the icon name (from the framework's icon catalog).</summary>
    public WorkspaceBuilder Icon(string icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        _icon = icon;
        return this;
    }

    /// <summary>Display order in the global workspace list.</summary>
    public WorkspaceBuilder Order(int order)
    {
        _order = order;
        return this;
    }

    /// <summary>
    /// Permission required to enter the workspace. When unset, the workspace is
    /// implicitly visible to every authenticated user; per-item permissions still
    /// gate individual entries (defense in depth).
    /// </summary>
    public WorkspaceBuilder RequiresPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        _requiresPermission = permission;
        return this;
    }

    /// <summary>
    /// Marks the workspace as a "shell" — declared by name only, intended to be
    /// populated by <see cref="IFeatureProvider"/> implementations from
    /// other modules. Empty shells are auto-filtered from the rendered tree.
    /// </summary>
    public WorkspaceBuilder Shell()
    {
        _isShell = true;
        return this;
    }

    /// <summary>
    /// Adds a section — sections are the collapsible groups within the workspace.
    /// Names must be unique per workspace.
    /// </summary>
    public WorkspaceBuilder Section(string key, Action<WorkspaceSectionBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(configure);

        WorkspaceSectionBuilder builder = new(key);
        configure(builder);
        _sectionFactories.Add(builder.Build);
        return this;
    }

    internal WorkspaceDescriptor Build(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        IReadOnlyList<WorkspaceSectionDescriptor> sections = [.. _sectionFactories.Select(f => f())];
        AssertUniqueSectionKeys(sections);

        return new WorkspaceDescriptor
        {
            Name = name,
            DisplayKey = _displayKey,
            Icon = _icon,
            Order = _order,
            RequiresPermission = _requiresPermission,
            Sections = sections,
            IsShell = _isShell,
        };
    }

    private static void AssertUniqueSectionKeys(IReadOnlyList<WorkspaceSectionDescriptor> sections)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (WorkspaceSectionDescriptor section in sections)
        {
            if (!seen.Add(section.Key))
            {
                throw new InvalidOperationException(
                    $"Duplicate workspace section key '{section.Key}'. Section keys must be unique within a workspace.");
            }
        }
    }
}
