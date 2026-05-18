namespace Granit.Workspaces;

/// <summary>
/// Fluent builder for one section of a workspace.
/// </summary>
public sealed class WorkspaceSectionBuilder
{
    private readonly string _key;
    private string? _displayKey;
    private int _order;
    private bool _collapsedByDefault;
    private readonly List<Func<WorkspaceItemDescriptor>> _itemFactories = [];
    private WorkspaceIncludeSpec? _dynamicInclude;

    internal WorkspaceSectionBuilder(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _key = key;
    }

    /// <summary>i18n key for the section header.</summary>
    public WorkspaceSectionBuilder DisplayKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _displayKey = key;
        return this;
    }

    /// <summary>Display order within the workspace.</summary>
    public WorkspaceSectionBuilder Order(int order)
    {
        _order = order;
        return this;
    }

    /// <summary>Start the section collapsed.</summary>
    public WorkspaceSectionBuilder CollapsedByDefault(bool collapsed = true)
    {
        _collapsedByDefault = collapsed;
        return this;
    }

    /// <summary>
    /// Adds an Entity item — references an <c>EntityDefinition</c> by its wire
    /// identifier. Pass a typed selector via <see cref="Entity{TEntity}"/> when
    /// you have a CLR reference to the definition.
    /// </summary>
    public WorkspaceSectionBuilder Entity(string entityName, Action<WorkspaceItemBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        WorkspaceItemBuilder builder = new(WorkspaceItemKind.Entity, entityName: entityName);
        configure?.Invoke(builder);
        _itemFactories.Add(builder.Build);
        return this;
    }

    /// <summary>
    /// Adds an Entity item by typed reference to its <c>EntityDefinition</c>.
    /// The wire identifier is resolved at boot time via the entity registry.
    /// Use this when the calling module has a static reference to the definition
    /// type (vs. <see cref="Entity(string, Action{WorkspaceItemBuilder}?)"/> for
    /// loosely-coupled string references).
    /// </summary>
    public WorkspaceSectionBuilder Entity<TEntityDefinition>(Action<WorkspaceItemBuilder>? configure = null)
        where TEntityDefinition : class =>
        Entity(typeof(TEntityDefinition).FullName ?? typeof(TEntityDefinition).Name, configure);

    /// <summary>Adds a Dashboard item — references a <c>DashboardDefinition</c> by its wire identifier.</summary>
    public WorkspaceSectionBuilder Dashboard(string dashboardName, Action<WorkspaceItemBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dashboardName);
        WorkspaceItemBuilder builder = new(WorkspaceItemKind.Dashboard, dashboardName: dashboardName);
        configure?.Invoke(builder);
        _itemFactories.Add(builder.Build);
        return this;
    }

    /// <summary>Adds a hyperlink item — internal SPA route or external URL.</summary>
    public WorkspaceSectionBuilder Link(string url, Action<WorkspaceItemBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        WorkspaceItemBuilder builder = new(WorkspaceItemKind.Link, linkUrl: url);
        configure?.Invoke(builder);
        _itemFactories.Add(builder.Build);
        return this;
    }

    /// <summary>Adds a sub-workspace reference by wire identifier.</summary>
    public WorkspaceSectionBuilder SubWorkspace(string workspaceName, Action<WorkspaceItemBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceName);
        WorkspaceItemBuilder builder = new(WorkspaceItemKind.SubWorkspace, subWorkspaceName: workspaceName);
        configure?.Invoke(builder);
        _itemFactories.Add(builder.Build);
        return this;
    }

    /// <summary>
    /// Adds a feature reference by catalog name (per ADR-057). The composer
    /// resolves the feature at filter time, lifting the feature's permission,
    /// route name, display key, and default icon into the payload. The optional
    /// <paramref name="configure"/> callback applies per-placement overrides
    /// (icon, order, route name, displayed-as label) without mutating the
    /// catalog entry. The feature must be declared by an
    /// <see cref="IFeatureProvider"/> registered at boot time; missing features
    /// cause startup to fail fast (no silent drop).
    /// </summary>
    public WorkspaceSectionBuilder Feature(string featureName, Action<WorkspaceItemBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);
        WorkspaceItemBuilder builder = new(WorkspaceItemKind.Feature, featureName: featureName);
        configure?.Invoke(builder);
        _itemFactories.Add(builder.Build);
        return this;
    }

    /// <summary>
    /// Dynamically absorbs every registered workspace whose name satisfies
    /// <paramref name="namePredicate"/> as a <see cref="WorkspaceItemKind.SubWorkspace"/>
    /// item (per ADR-057 §3). The composer resolves the predicate at boot
    /// against the workspace registry and synthesises one item per matching
    /// workspace, lifting the target workspace's icon / display key / order —
    /// no duplication between the including section and the included shell.
    /// A workspace dropped as an empty shell produces no item; dangling
    /// sub-workspace references are therefore impossible by construction.
    /// </summary>
    public WorkspaceSectionBuilder IncludeWorkspacesMatching(Predicate<string> namePredicate)
    {
        ArgumentNullException.ThrowIfNull(namePredicate);
        if (_dynamicInclude is not null)
        {
            throw new InvalidOperationException(
                $"Section '{_key}' already declared a dynamic include — only one " +
                "IncludeWorkspacesMatching call per section is supported (per ADR-057 §3).");
        }
        _dynamicInclude = new WorkspaceIncludeSpec(namePredicate);
        return this;
    }

    internal WorkspaceSectionDescriptor Build()
    {
        IReadOnlyList<WorkspaceItemDescriptor> items =
            [.. _itemFactories.Select(f => f()).OrderBy(i => i.Order)];

        return new WorkspaceSectionDescriptor(
            _key,
            _displayKey,
            _order,
            _collapsedByDefault,
            items,
            _dynamicInclude);
    }
}
