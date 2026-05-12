using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.Internal;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Timing;

namespace Granit.Analytics.Rendering;

/// <summary>
/// <see cref="IWidgetInstanceRenderer"/> for the <c>"Map"</c> widget kind.
/// Resolves the registered <see cref="IMapRunner"/> by query name, runs it
/// with the configured latitude/longitude columns plus the popup whitelist,
/// and shapes the result into a <see cref="MapWidgetSnapshot"/>. Per-widget
/// permission gate + error isolation already apply upstream
/// (<see cref="IDashboardRenderer"/> / ADR-039 §3); this renderer is
/// responsible for the body shape only.
/// </summary>
/// <remarks>
/// B7-2 ships <see cref="MapPointSource.LatLng"/> (decimal lat/lng columns,
/// works on any database). The PostGIS <see cref="MapPointSource.Geography"/>
/// path is deferred — the renderer surfaces
/// <c>Widget:Unavailable.MapGeographyNotImplemented</c> until the NetTopologySuite
/// adapter ships in a future <c>granit-iot</c> package (the spatial column type
/// is the IoT-specific dependency, not the rendering itself).
/// </remarks>
internal sealed class MapWidgetInstanceRenderer(
    MapService mapService,
    IClock clock) : IWidgetInstanceRenderer
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly MapService _mapService = mapService;
    private readonly IClock _clock = clock;

    public string WidgetType => "Map";

    public async Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(widget);

        MapConfig config = JsonSerializer.Deserialize<MapConfig>(widget.ConfigJson, ConfigJsonOptions)
            ?? throw new InvalidOperationException(
                $"Widget {widget.Id} ('Map') has empty ConfigJson — map config cannot be resolved.");

        if (string.IsNullOrWhiteSpace(widget.QueryName))
        {
            throw new InvalidOperationException(
                $"Widget {widget.Id} ('Map') has no QueryName — Map widgets must reference a registered QueryDefinition.");
        }

        DateTimeOffset emittedAt = _clock.Now;

        if (!_mapService.TryGetRunner(widget.QueryName, out IMapRunner runner))
        {
            return WidgetSnapshotEnvelope.Unavailable(
                widgetType: WidgetType,
                sequence: 1,
                emittedAt: emittedAt,
                refreshHint: RefreshHint.Static,
                reasonLocalizationKey: "Widget:Unavailable.QueryNotFound");
        }

        // PostGIS path requires the host to pull `Granit.Analytics.PostGIS` —
        // the runner reports SupportsGeography only when an
        // IGeographyPointProjector<TEntity> is registered. Without it, surface
        // the typed Unavailable so the dashboard keeps rendering the rest.
        if (config.PointSource is MapPointSource.Geography && !runner.SupportsGeography)
        {
            return WidgetSnapshotEnvelope.Unavailable(
                widgetType: WidgetType,
                sequence: 1,
                emittedAt: emittedAt,
                refreshHint: RefreshHint.Static,
                reasonLocalizationKey: "Widget:Unavailable.MapGeographyNotImplemented");
        }

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: config.PointSource,
            popupColumns: config.PopupColumns,
            dashboardFilters: context.DashboardFilters,
            cancellationToken).ConfigureAwait(false);

        MapCenterPayload? defaultCenter = config.DefaultCenter is { } center
            ? new MapCenterPayload(center.Latitude, center.Longitude)
            : null;

        MapWidgetSnapshot snapshot = new(
            Points: [.. result.Points.Select(p => new MapPoint(p.Id, p.Latitude, p.Longitude, p.Popup))],
            DefaultZoom: config.DefaultZoom,
            DefaultCenter: defaultCenter,
            ClusterThreshold: config.ClusterThreshold,
            DetailRoute: config.DetailRoute,
            TileUrlTemplate: config.TileUrlTemplate,
            DefaultLayerKind: config.DefaultLayerKind);

        return WidgetSnapshotEnvelope.ForSnapshot(
            widgetType: WidgetType,
            snapshot: JsonSerializer.SerializeToElement(snapshot, SnapshotJsonOptions),
            sequence: 1,
            emittedAt: emittedAt,
            refreshHint: RefreshHint.Dynamic);
    }

    private sealed record MapConfig(
        MapPointSource PointSource,
        IReadOnlyList<string>? PopupColumns,
        int DefaultZoom,
        MapCenterConfig? DefaultCenter,
        int ClusterThreshold,
        string? DetailRoute,
        string? TileUrlTemplate,
        MapTileLayerKind? DefaultLayerKind = null);

    /// <summary>
    /// Wire shape for <see cref="MapWidgetDefinition.DefaultCenter"/> in the
    /// persisted ConfigJson — a plain pair of doubles, no validation here
    /// (the import path validated against <see cref="MapCenter"/>).
    /// </summary>
    private sealed record MapCenterConfig(double Latitude, double Longitude);
}
