namespace Granit.Workspaces;

/// <summary>
/// Fluent builder for one item within a <see cref="WorkspaceSectionBuilder"/>.
/// </summary>
public sealed class WorkspaceItemBuilder
{
    private readonly WorkspaceItemKind _kind;
    private readonly string? _entityName;
    private readonly string? _dashboardName;
    private readonly string? _linkUrl;
    private readonly string? _subWorkspaceName;
    private readonly string? _featureName;

    private string? _entityViewName;
    private IReadOnlyDictionary<string, object?>? _entityPresetOverlay;
    private string? _displayKey;
    private string? _icon;
    private int _order;
    private string? _routeName;
    private string? _requiresPermission;

    internal WorkspaceItemBuilder(
        WorkspaceItemKind kind,
        string? entityName = null,
        string? dashboardName = null,
        string? linkUrl = null,
        string? subWorkspaceName = null,
        string? featureName = null)
    {
        _kind = kind;
        _entityName = entityName;
        _dashboardName = dashboardName;
        _linkUrl = linkUrl;
        _subWorkspaceName = subWorkspaceName;
        _featureName = featureName;
    }

    /// <summary>i18n key for the item label.</summary>
    public WorkspaceItemBuilder DisplayKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _displayKey = key;
        return this;
    }

    /// <summary>Override the icon shown next to the item.</summary>
    public WorkspaceItemBuilder Icon(string icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        _icon = icon;
        return this;
    }

    /// <summary>Display order within the section (lower first).</summary>
    public WorkspaceItemBuilder Order(int order)
    {
        _order = order;
        return this;
    }

    /// <summary>
    /// Permission required for the item to surface in the manifest. When the user
    /// does not hold it, the item is dropped from the payload entirely (defense
    /// in depth, ADR-040 §6).
    /// </summary>
    public WorkspaceItemBuilder RequiresPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        _requiresPermission = permission;
        return this;
    }

    /// <summary>Sets the preferred default <c>EntityView</c> name (e.g. <c>"open"</c>) for an Entity item.</summary>
    public WorkspaceItemBuilder View(string viewName)
    {
        EnsureKind(WorkspaceItemKind.Entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
        _entityViewName = viewName;
        return this;
    }

    /// <summary>
    /// Sets an additive preset overlay for an Entity item (per ADR-048 §4). The
    /// overlay carries extra filters / sort / columns layered on top of the
    /// entity's compiled defaults; runtime validation enforces additive-only
    /// semantics — the overlay can constrain, never relax.
    /// </summary>
    public WorkspaceItemBuilder Preset(IReadOnlyDictionary<string, object?> preset)
    {
        EnsureKind(WorkspaceItemKind.Entity);
        ArgumentNullException.ThrowIfNull(preset);
        _entityPresetOverlay = preset;
        return this;
    }

    /// <summary>
    /// Overrides the route name resolved against the host's React route table
    /// for a <see cref="WorkspaceItemKind.Feature"/> item (per ADR-057 §5).
    /// When unset, the composer falls back to the feature's catalog-declared route name.
    /// </summary>
    public WorkspaceItemBuilder RouteName(string routeName)
    {
        EnsureKind(WorkspaceItemKind.Feature);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);
        _routeName = routeName;
        return this;
    }

    internal WorkspaceItemDescriptor Build() =>
        new(
            _kind,
            _order,
            _displayKey,
            _icon,
            _entityName,
            _entityViewName,
            _entityPresetOverlay,
            _dashboardName,
            _linkUrl,
            _subWorkspaceName,
            _featureName,
            _routeName,
            _requiresPermission);

    private void EnsureKind(WorkspaceItemKind expected)
    {
        if (_kind != expected)
        {
            throw new InvalidOperationException(
                $"This configuration is only valid on {expected} items; this builder targets a {_kind} item.");
        }
    }
}
