namespace Granit.Analytics.Dashboards.Widgets;

/// <summary>
/// Logical kind of map tile layer a <see cref="MapWidgetDefinition"/> wants
/// to default to. Decoupled from any specific tile provider id (OSM, SPW
/// Wallonia, ArcGIS, …) so a widget can declare "show satellite imagery"
/// without binding to a vendor — the frontend picks the actual layer URL
/// from whichever <c>MapTileProvider</c> the host has registered.
/// </summary>
/// <remarks>
/// <para>
/// Wire format is PascalCase per ADR-039 §6.1 — the framework's standard
/// <c>JsonStringEnumConverter()</c> uses the default naming policy, so
/// <see cref="Plan"/> serialises as <c>"Plan"</c>. Frontend mirror lives in
/// <c>granit-front/packages/@granit/react-map/src/types.ts</c> as the
/// TypeScript union <c>'Plan' | 'Satellite' | 'Hybrid' | 'Topo' | 'Custom'</c>.
/// </para>
/// <para>
/// Resolution rule on the frontend: when the active <c>MapTileProvider</c>
/// has a layer matching the requested kind, that layer becomes active;
/// otherwise the provider's first layer is used (graceful fallback).
/// </para>
/// </remarks>
public enum MapTileLayerKind
{
    /// <summary>Cartographic / road-map style — typically the default for navigation use cases (parcel maps, address pickers).</summary>
    Plan = 0,

    /// <summary>Aerial / orthophoto imagery — best for delivery tracking, infrastructure inspection, agricultural overlays.</summary>
    Satellite = 1,

    /// <summary>Satellite imagery overlaid with road / label vectors — combines the recognisability of <see cref="Plan"/> with the realism of <see cref="Satellite"/>.</summary>
    Hybrid = 2,

    /// <summary>Topographic style — relief shading, contour lines, terrain. Useful for outdoor / GIS / hiking-style use cases.</summary>
    Topo = 3,

    /// <summary>Provider-specific layer that doesn't fit the standard kinds. The frontend treats it as opaque and uses whichever layer the provider exposes under that name.</summary>
    Custom = 4,
}
