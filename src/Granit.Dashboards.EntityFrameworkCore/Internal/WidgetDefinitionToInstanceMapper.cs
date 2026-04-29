using System.Text.Json;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.Dashboards.Widgets;

namespace Granit.Dashboards.EntityFrameworkCore.Internal;

/// <summary>
/// Translates a typed <see cref="WidgetDefinition"/> shipped by a module into the
/// persisted <c>WidgetInstance</c> shape — discriminator string, denormalised
/// metric / query references for archi cross-checks, plus the kind-specific JSON
/// payload. Used by the import endpoint to deep-copy a <c>DashboardDefinition</c>
/// into a tenant-scoped <c>Dashboard</c> aggregate.
/// </summary>
internal static class WidgetDefinitionToInstanceMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Result of the mapping — fed into <c>Dashboard.AddWidget(...)</c>.</summary>
    internal sealed record Mapping(
        string WidgetType,
        string? MetricName,
        string? QueryName,
        string ConfigJson);

    /// <summary>
    /// Maps the supplied <paramref name="widget"/> to a persisted-shape tuple.
    /// Throws <see cref="InvalidOperationException"/> for unknown widget kinds —
    /// the caller is expected to ship its own derived widget records through the
    /// runtime polymorphism extension (P1.1) and add a corresponding case here.
    /// </summary>
    public static Mapping Map(WidgetDefinition widget) => widget switch
    {
        MarkdownWidgetDefinition m => new(
            "Markdown", null, null,
            JsonSerializer.Serialize(new { contentLocalizationKey = m.ContentLocalizationKey }, JsonOptions)),

        ImageWidgetDefinition i => new(
            "Image", null, null,
            JsonSerializer.Serialize(
                new { source = i.Source, altLocalizationKey = i.AltLocalizationKey, fit = i.Fit.ToString() },
                JsonOptions)),

        TextWidgetDefinition t => new(
            "Text", null, null,
            JsonSerializer.Serialize(
                new { contentLocalizationKey = t.ContentLocalizationKey, style = t.Style.ToString() },
                JsonOptions)),

        KpiWidgetDefinition k => new(
            "Kpi",
            ExtractMetricName(k.Datasource),
            ExtractQueryName(k.Datasource),
            // The full Datasource (including KeyFormats / aggregation parameters) lands
            // in ConfigJson — the typed names extracted above are denormalised columns
            // for cross-widget reference checks (DashboardWidgetReferenceTests archi rule).
            JsonSerializer.Serialize<Datasource>(k.Datasource, JsonOptions)),

        ChartWidgetDefinition c => new(
            "Chart", null, c.QueryName,
            JsonSerializer.Serialize(
                new
                {
                    groupBy = c.GroupBy,
                    aggregation = c.Aggregation.ToString(),
                    field = c.Field,
                    chartType = c.ChartType.ToString(),
                },
                JsonOptions)),

        TableWidgetDefinition t => new(
            "Table", null, t.QueryName,
            JsonSerializer.Serialize(
                new { visibleColumns = t.VisibleColumns, pageSize = t.PageSize },
                JsonOptions)),

        PivotWidgetDefinition p => new(
            "Pivot", null, p.QueryName,
            JsonSerializer.Serialize(
                new
                {
                    rowFields = p.RowFields,
                    columnFields = p.ColumnFields,
                    valueField = p.ValueField,
                    valueAggregation = p.ValueAggregation.ToString(),
                },
                JsonOptions)),

        MapWidgetDefinition map => new(
            "Map", null, map.QueryName,
            JsonSerializer.Serialize(
                new
                {
                    pointSource = SerializeMapPointSource(map.PointSource),
                    popupColumns = map.PopupColumns,
                    defaultZoom = map.DefaultZoom,
                    defaultCenter = map.DefaultCenter is { } c
                        ? new { latitude = c.Latitude, longitude = c.Longitude }
                        : null,
                    clusterThreshold = map.ClusterThreshold,
                    detailRoute = map.DetailRoute,
                    tileUrlTemplate = map.TileUrlTemplate,
                    // B7-3 (#1577) — preferred layer kind is part of the persisted
                    // contract; the renderer round-trips it onto MapWidgetSnapshot
                    // and the frontend resolves the matching layer from whichever
                    // MapTileProvider the host registered.
                    defaultLayerKind = map.DefaultLayerKind?.ToString(),
                },
                JsonOptions)),

        _ => throw new InvalidOperationException(
            $"No persisted-shape mapping registered for widget kind '{widget.GetType().Name}'. "
            + "Custom widget kinds shipped by downstream packages (Granit.IoT.Dashboards, ...) "
            + "must extend WidgetDefinitionToInstanceMapper before they can be imported."),
    };

    private static string? ExtractMetricName(Datasource datasource) => datasource switch
    {
        MetricDatasource md => md.MetricName,
        _ => null,
    };

    private static string? ExtractQueryName(Datasource datasource) => datasource switch
    {
        QueryAggregateDatasource qa => qa.QueryName,
        _ => null,
    };

    private static object SerializeMapPointSource(MapPointSource source) => source switch
    {
        MapPointSource.LatLng latLng => new
        {
            kind = "lat-lng",
            latitudeColumn = latLng.LatitudeColumn,
            longitudeColumn = latLng.LongitudeColumn,
        },
        MapPointSource.Geography geo => new
        {
            kind = "geography",
            geographyColumn = geo.GeographyColumn,
        },
        _ => throw new InvalidOperationException(
            $"Unknown MapPointSource subtype '{source.GetType().Name}'."),
    };
}
