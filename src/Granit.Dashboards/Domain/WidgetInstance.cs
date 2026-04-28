using Granit.Dashboards.Domain.ValueObjects;
using Granit.Domain;

namespace Granit.Dashboards.Domain;

/// <summary>
/// A single widget pinned on a persisted <see cref="Dashboard"/>. Owned by the
/// dashboard aggregate — never mutated outside the parent's behaviour methods.
/// Per ADR-038, kind-specific configuration is stored as a JSON blob (<see cref="ConfigJson"/>)
/// to keep the schema additive when new widget kinds ship.
/// </summary>
public sealed class WidgetInstance : Entity
{
    // Parameterless constructor required by EF Core materializer.
    private WidgetInstance() { }

    /// <summary>Strongly-typed identifier (backed by <see cref="Entity.Id"/>).</summary>
    public WidgetInstanceId WidgetInstanceId => WidgetInstanceId.Create(Id);

    /// <summary>Owning dashboard identifier (foreign key, set by the parent aggregate on add).</summary>
    public Guid DashboardId { get; private set; }

    /// <summary>
    /// Discriminator: <c>"Kpi"</c>, <c>"Chart"</c>, <c>"Table"</c>, <c>"Pivot"</c>,
    /// <c>"Markdown"</c>, <c>"Image"</c>, <c>"Text"</c>, ... (extensible). Drives
    /// the per-kind <see cref="ConfigJson"/> schema interpreted by the frontend.
    /// </summary>
    public string WidgetType { get; private set; } = string.Empty;

    /// <summary>Dense-ranked grid order — 0-based, contiguous within the dashboard.</summary>
    public int Position { get; private set; }

    /// <summary>Grid columns occupied by the widget.</summary>
    public int Width { get; private set; }

    /// <summary>Grid rows occupied by the widget.</summary>
    public int Height { get; private set; }

    /// <summary>
    /// Wire identifier of the metric backing this widget (KPI). <c>null</c> for non-metric kinds.
    /// </summary>
    public string? MetricName { get; private set; }

    /// <summary>
    /// Wire identifier of the query backing this widget (Chart / Table / Pivot). <c>null</c> for non-query kinds.
    /// </summary>
    public string? QueryName { get; private set; }

    /// <summary>
    /// Kind-specific configuration as JSON. Schema interpreted per <see cref="WidgetType"/>.
    /// Validated server-side by per-kind validators (story B3 #1384).
    /// </summary>
    public string ConfigJson { get; private set; } = "{}";

    /// <summary>Localization key for the widget's title (<c>Widget:{DashboardName}.{Slug}</c> by convention).</summary>
    public string TitleLocalizationKey { get; private set; } = string.Empty;

    /// <summary>
    /// Optional override of the permission gating this widget at render time. When
    /// <c>null</c>, the runtime resolves the effective permission from the underlying
    /// metric / query.
    /// </summary>
    public string? RequiredPermission { get; private set; }

    /// <summary>
    /// Per-instance presentation overrides applied on top of the imported config —
    /// title, colour, unit, decimals, threshold rules. <c>null</c> = the widget renders
    /// strictly from <see cref="ConfigJson"/> + the data source's declared formatting.
    /// Mutated through <see cref="ApplyOverrides(WidgetInstanceConfig?)"/>.
    /// See P3.2 of the dashboards-architecture-proposals roadmap.
    /// </summary>
    public WidgetInstanceConfig? Overrides { get; private set; }

    /// <summary>Creates a new <see cref="WidgetInstance"/>. Called only by <see cref="Dashboard"/>.</summary>
    internal static WidgetInstance Create(
        Guid id,
        Guid dashboardId,
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
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(titleLocalizationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(configJson);

        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be >= 0.");
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be > 0.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be > 0.");
        }

        return new WidgetInstance
        {
            Id = id,
            DashboardId = dashboardId,
            WidgetType = widgetType,
            Position = position,
            Width = width,
            Height = height,
            TitleLocalizationKey = titleLocalizationKey,
            ConfigJson = configJson,
            MetricName = metricName,
            QueryName = queryName,
            RequiredPermission = requiredPermission,
        };
    }

    /// <summary>Updates the layout (position / size). Called by the aggregate during reorder.</summary>
    internal void Reposition(int position, int width, int height)
    {
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be >= 0.");
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be > 0.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be > 0.");
        }

        Position = position;
        Width = width;
        Height = height;
    }

    /// <summary>Replaces the kind-specific JSON configuration. Called by the aggregate on widget edit.</summary>
    internal void UpdateConfig(string configJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configJson);
        ConfigJson = configJson;
    }

    /// <summary>Updates the localization key used to render the widget's title.</summary>
    internal void UpdateTitle(string titleLocalizationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titleLocalizationKey);
        TitleLocalizationKey = titleLocalizationKey;
    }

    /// <summary>
    /// Applies (or clears) the per-instance presentation overrides. Pass <c>null</c>
    /// to revert to the imported defaults.
    /// </summary>
    public void ApplyOverrides(WidgetInstanceConfig? overrides) => Overrides = overrides;
}
