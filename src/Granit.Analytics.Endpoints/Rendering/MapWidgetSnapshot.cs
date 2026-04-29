using System.Text.Json;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// Wire-shape snapshot for the <c>"Map"</c> widget kind. Carries the rendered
/// markers plus the camera / clustering / detail-route hints the frontend
/// needs to bootstrap the Leaflet (or equivalent) map without a second HTTP
/// round-trip.
/// </summary>
/// <param name="Points">Geocoded markers in stream-arrival order. Cap is bounded by the QueryDefinition's <c>MaxStreamSize</c> upstream.</param>
/// <param name="DefaultZoom">Initial zoom level (Leaflet scale: 0 world → 18 building).</param>
/// <param name="DefaultCenter">Initial map center; <see langword="null"/> means the frontend should fit the bounding box of <see cref="Points"/>.</param>
/// <param name="ClusterThreshold">Marker count above which the frontend activates clustering.</param>
/// <param name="DetailRoute">Optional route template invoked on marker click (<c>{id}</c> substituted with the row's primary key).</param>
/// <param name="TileUrlTemplate">Optional Leaflet tile-URL override; <see langword="null"/> falls back to the host's default (typically OpenStreetMap).</param>
public sealed record MapWidgetSnapshot(
    IReadOnlyList<MapPoint> Points,
    int DefaultZoom,
    MapCenterPayload? DefaultCenter,
    int ClusterThreshold,
    string? DetailRoute,
    string? TileUrlTemplate);

/// <summary>One marker on the map.</summary>
/// <param name="Id">Entity primary key when the entity exposes a <c>Guid Id</c> property; <see langword="null"/> otherwise. Drives the click-through to <c>DetailRoute</c>.</param>
/// <param name="Latitude">Latitude in decimal degrees.</param>
/// <param name="Longitude">Longitude in decimal degrees.</param>
/// <param name="Popup">Whitelisted column subset for the marker popup body. <see langword="null"/> when no popup columns were configured on the widget.</param>
public sealed record MapPoint(
    Guid? Id,
    double Latitude,
    double Longitude,
    JsonElement? Popup);

/// <summary>Wire shape for <see cref="MapWidgetSnapshot.DefaultCenter"/>.</summary>
/// <param name="Latitude">Latitude in decimal degrees.</param>
/// <param name="Longitude">Longitude in decimal degrees.</param>
public sealed record MapCenterPayload(double Latitude, double Longitude);
