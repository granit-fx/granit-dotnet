using Granit.Dashboards.Domain.Events;
using Granit.Dashboards.Domain.ValueObjects;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Dashboards.Domain;

/// <summary>
/// Tenant-composed dashboard. Persisted counterpart of <see cref="DashboardDefinition"/>
/// (which lives in code, in <c>Granit.Dashboards.Abstractions</c>). When an admin
/// imports a definition (story B4), the framework deep-copies it into a new
/// <see cref="Dashboard"/> aggregate; module upgrades surface drift but never
/// silently retro-edit (ADR-038 §3).
/// </summary>
/// <remarks>
/// State machine — strict and one-way except <see cref="DashboardStatus.Archived"/>
/// which can be restored to <see cref="DashboardStatus.Draft"/>:
///
/// <code>
///   Draft ──Publish──▶ Published
///     ▲                    │
///     │                  Archive
///     │                    ▼
///     └─Restore──── Archived
/// </code>
/// </remarks>
public sealed class Dashboard : FullAuditedAggregateRoot, IMultiTenant
{
    private readonly List<WidgetInstance> _widgets = [];

    // Parameterless constructor required by EF Core materializer.
    private Dashboard() { }

    /// <summary>Strongly-typed identifier (backed by <see cref="FullAuditedAggregateRoot.Id"/>).</summary>
    public DashboardId DashboardId => DashboardId.Create(Id);

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Tenant-renamable display name surfaced in the catalogue.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Coarse grouping — drives the catalogue ordering surfaced to admins.</summary>
    public DashboardCategory Category { get; private set; }

    /// <summary>Lifecycle state — see <see cref="DashboardStatus"/>.</summary>
    public DashboardStatus Status { get; private set; }

    /// <summary>
    /// Wire identifier of the <c>DashboardDefinition</c> this dashboard was imported from.
    /// <c>null</c> for ad-hoc dashboards composed entirely in the UI.
    /// </summary>
    public string? SourceDefinitionName { get; private set; }

    /// <summary>
    /// Semver of the source definition at import time. Used by the drift-detection UI
    /// (story B4) to flag dashboards whose upstream definition has been updated since
    /// the import. <c>null</c> for ad-hoc dashboards.
    /// </summary>
    public string? SourceDefinitionVersion { get; private set; }

    /// <summary>
    /// Whether the dashboard is mandated by the platform operator (cannot be deleted by
    /// tenant admins, only re-synced). Mirrors <c>DashboardDefinition.IsSystem</c> at
    /// import time.
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>Grid columns — typically 12 (responsive grid).</summary>
    public int LayoutColumns { get; private set; }

    /// <summary>Grid row height in CSS pixels — typically 80.</summary>
    public int LayoutRowHeight { get; private set; }

    /// <summary>Widgets pinned on the dashboard, in declared <see cref="WidgetInstance.Position"/> order.</summary>
    public IReadOnlyList<WidgetInstance> Widgets => _widgets;

    /// <summary>
    /// Creates a new <see cref="Dashboard"/> in <see cref="DashboardStatus.Draft"/>.
    /// </summary>
    public static Dashboard Create(
        Guid id,
        string name,
        DashboardCategory category,
        int layoutColumns = 12,
        int layoutRowHeight = 80,
        Guid? tenantId = null,
        string? sourceDefinitionName = null,
        string? sourceDefinitionVersion = null,
        bool isSystem = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (layoutColumns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layoutColumns), "Layout columns must be > 0.");
        }

        if (layoutRowHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layoutRowHeight), "Layout row height must be > 0.");
        }

        var dashboard = new Dashboard
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            Category = category,
            Status = DashboardStatus.Draft,
            SourceDefinitionName = sourceDefinitionName,
            SourceDefinitionVersion = sourceDefinitionVersion,
            IsSystem = isSystem,
            LayoutColumns = layoutColumns,
            LayoutRowHeight = layoutRowHeight,
        };

        dashboard.AddDomainEvent(new DashboardCreatedEvent(id, tenantId, name, sourceDefinitionName));
        return dashboard;
    }

    /// <summary>Renames the dashboard and raises <see cref="DashboardModifiedEvent"/>.</summary>
    public void Rename(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        if (Name == newName)
        {
            return;
        }

        Name = newName;
        AddDomainEvent(new DashboardModifiedEvent(Id, TenantId));
    }

    /// <summary>Updates the grid layout and raises <see cref="DashboardModifiedEvent"/>.</summary>
    public void UpdateLayout(int layoutColumns, int layoutRowHeight)
    {
        if (layoutColumns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layoutColumns), "Layout columns must be > 0.");
        }

        if (layoutRowHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layoutRowHeight), "Layout row height must be > 0.");
        }

        if (LayoutColumns == layoutColumns && LayoutRowHeight == layoutRowHeight)
        {
            return;
        }

        LayoutColumns = layoutColumns;
        LayoutRowHeight = layoutRowHeight;
        AddDomainEvent(new DashboardModifiedEvent(Id, TenantId));
    }

    /// <summary>
    /// Adds a widget to the dashboard. The widget's <c>DashboardId</c> is set automatically;
    /// <c>Position</c> is appended at the end if not already taken.
    /// </summary>
    public WidgetInstance AddWidget(
        Guid widgetId,
        string widgetType,
        int position,
        int width,
        int height,
        string titleLocalizationKey,
        string configJson,
        string? metricName = null,
        string? queryName = null,
        string? requiredPermission = null)
    {
        var widget = WidgetInstance.Create(
            widgetId,
            Id,
            widgetType,
            position,
            width,
            height,
            titleLocalizationKey,
            configJson,
            metricName,
            queryName,
            requiredPermission);

        _widgets.Add(widget);
        AddDomainEvent(new DashboardModifiedEvent(Id, TenantId));
        return widget;
    }

    /// <summary>Removes a widget by id. No-op when the widget is not pinned.</summary>
    public void RemoveWidget(Guid widgetId)
    {
        WidgetInstance? widget = _widgets.FirstOrDefault(w => w.Id == widgetId);
        if (widget is null)
        {
            return;
        }

        _widgets.Remove(widget);
        AddDomainEvent(new DashboardModifiedEvent(Id, TenantId));
    }

    /// <summary>
    /// Updates the layout (position / size), title localization key, and JSON
    /// configuration of a pinned widget. WidgetType, MetricName, QueryName and
    /// RequiredPermission are intentionally immutable here — switching widget
    /// kind or rebinding to a different metric/query is delete + add, not edit.
    /// Returns <c>true</c> when the widget existed; <c>false</c> when it was
    /// not pinned.
    /// </summary>
    public bool UpdateWidget(
        Guid widgetId,
        int position,
        int width,
        int height,
        string titleLocalizationKey,
        string configJson)
    {
        WidgetInstance? widget = _widgets.FirstOrDefault(w => w.Id == widgetId);
        if (widget is null)
        {
            return false;
        }

        widget.Reposition(position, width, height);
        widget.UpdateTitle(titleLocalizationKey);
        widget.UpdateConfig(configJson);

        AddDomainEvent(new DashboardModifiedEvent(Id, TenantId));
        return true;
    }

    /// <summary>Transitions <see cref="DashboardStatus.Draft"/> → <see cref="DashboardStatus.Published"/>.</summary>
    public void Publish()
    {
        if (Status == DashboardStatus.Published)
        {
            return;
        }

        if (Status == DashboardStatus.Archived)
        {
            throw new InvalidOperationException("An archived dashboard must be restored before it can be published.");
        }

        Status = DashboardStatus.Published;
        AddDomainEvent(new DashboardPublishedEvent(Id, TenantId));
    }

    /// <summary>Transitions to <see cref="DashboardStatus.Archived"/>.</summary>
    public void Archive()
    {
        if (Status == DashboardStatus.Archived)
        {
            return;
        }

        Status = DashboardStatus.Archived;
        AddDomainEvent(new DashboardArchivedEvent(Id, TenantId));
    }

    /// <summary>Transitions <see cref="DashboardStatus.Archived"/> → <see cref="DashboardStatus.Draft"/>.</summary>
    public void Restore()
    {
        if (Status != DashboardStatus.Archived)
        {
            throw new InvalidOperationException("Only an archived dashboard can be restored.");
        }

        Status = DashboardStatus.Draft;
        AddDomainEvent(new DashboardModifiedEvent(Id, TenantId));
    }

    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }
}
