using System.Text.Json.Serialization;
using Granit.Dashboards;

namespace Granit.Analytics.Dashboards.Widgets;

/// <summary>
/// Describes how a <see cref="MapWidgetDefinition"/> reads coordinates from the
/// rows produced by its backing <c>QueryDefinition</c>.
/// </summary>
/// <remarks>
/// Two flavours are supported per B7 (#1404). The default <see cref="LatLng"/>
/// path maps to plain decimal columns and works on any database. The opt-in
/// <see cref="Geography"/> path reads a single <c>geography(Point)</c> column —
/// PostGIS-only, but unlocks server-side spatial filters
/// (<c>ST_DWithin</c>, <c>ST_Within</c>, …) in follow-up stories.
/// JSON polymorphism uses the <c>"kind"</c> discriminator (<c>"lat-lng"</c> /
/// <c>"geography"</c>) — same convention as <c>Datasource</c>.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LatLng), "lat-lng")]
[JsonDerivedType(typeof(Geography), "geography")]
public abstract record MapPointSource
{
    private MapPointSource() { }

    /// <summary>Column-pair source: rows expose a decimal latitude and longitude.</summary>
    public sealed record LatLng(string LatitudeColumn, string LongitudeColumn) : MapPointSource;

    /// <summary>PostGIS source: a single <c>geography(Point)</c> column.</summary>
    public sealed record Geography(string GeographyColumn) : MapPointSource;
}

/// <summary>
/// Map widget — renders geocoded query rows as markers on an interactive map.
/// Useful for any tenant with location data: customer addresses, branch offices,
/// delivery routes, sales-by-region. Orthogonal to IoT real-time tracking
/// (a future <c>granit-iot</c> dashboard module owns live device traces).
/// </summary>
/// <remarks>
/// <para>
/// Bound to a <c>QueryDefinition</c> by name (same pattern as
/// <see cref="TableWidgetDefinition"/>). The query's row shape determines how
/// coordinates are read via <see cref="PointSource"/> — column pair on any
/// database, or a single PostGIS <c>geography(Point)</c> column.
/// </para>
/// <para>
/// Configuration is intentionally narrow at v1: cluster threshold, default
/// camera (zoom + center), popup template, optional detail-route, optional
/// tile-URL override. Permission filtering and per-widget overrides come from
/// the parent <see cref="WidgetDefinition"/>.
/// </para>
/// </remarks>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="QueryName">The <c>QueryDefinition.Name</c> backing this map.</param>
/// <param name="PointSource">How rows expose coordinates — lat/lng pair or PostGIS geography column.</param>
/// <param name="PopupColumns">Whitelisted column names rendered in the marker popup, in order. <c>null</c> = no popup body beyond the entity id.</param>
/// <param name="DefaultZoom">Initial map zoom (Leaflet scale: 0 world → 18 building). Defaults to <c>5</c> (country level).</param>
/// <param name="DefaultCenter">Initial map center as <c>(latitude, longitude)</c>. Falls back to the bounding box of the rendered points when <c>null</c>.</param>
/// <param name="ClusterThreshold">Row count above which marker clustering activates automatically. Defaults to <c>200</c>.</param>
/// <param name="DetailRoute">Optional route template invoked on marker click — <c>{id}</c> is substituted with the row's primary key. Example: <c>/customers/{id}</c>.</param>
/// <param name="TileUrlTemplate">Optional Leaflet tile-URL template overriding the OpenStreetMap default. Frontend hosts MUST keep a visible attribution that matches the tile provider's licence.</param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.MediaTile"/> — maps usually want square real-estate.</param>
/// <param name="RequiredPermission">See <see cref="WidgetDefinition.RequiredPermission"/>.</param>
/// <param name="TimeWindowOverride">See <see cref="WidgetDefinition.TimeWindowOverride"/>.</param>
/// <param name="Actions">See <see cref="WidgetDefinition.Actions"/>.</param>
public sealed record MapWidgetDefinition(
    string Slug,
    string QueryName,
    MapPointSource PointSource,
    IReadOnlyList<string>? PopupColumns,
    int Position,
    int DefaultZoom = 5,
    MapCenter? DefaultCenter = null,
    int ClusterThreshold = 200,
    string? DetailRoute = null,
    string? TileUrlTemplate = null,
    WidgetSize? Size = null,
    string? RequiredPermission = null,
    DashboardTimeWindow? TimeWindowOverride = null,
    IReadOnlyList<WidgetAction>? Actions = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.MediaTile, RequiredPermission, TimeWindowOverride, Actions);

/// <summary>
/// Latitude / longitude pair used to seed a <see cref="MapWidgetDefinition.DefaultCenter"/>.
/// Coordinates outside <c>[-90, 90]</c> / <c>[-180, 180]</c> are rejected at construction.
/// </summary>
public sealed record MapCenter
{
    /// <summary>Latitude in decimal degrees, in <c>[-90, 90]</c>.</summary>
    public double Latitude { get; }

    /// <summary>Longitude in decimal degrees, in <c>[-180, 180]</c>.</summary>
    public double Longitude { get; }

    /// <summary>Creates a center, rejecting out-of-range coordinates.</summary>
    public MapCenter(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be within [-90, 90].");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be within [-180, 180].");
        }

        Latitude = latitude;
        Longitude = longitude;
    }
}
