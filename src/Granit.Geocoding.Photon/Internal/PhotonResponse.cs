using System.Text.Json.Serialization;

namespace Granit.Geocoding.Photon.Internal;

/// <summary>
/// The Photon <c>/api</c> response — a GeoJSON <c>FeatureCollection</c>. Only the geometry is read; the rich
/// textual properties of each feature are ignored (we need only the coordinate).
/// </summary>
internal sealed record PhotonResponse
{
    [JsonPropertyName("features")]
    public IReadOnlyList<PhotonFeature>? Features { get; init; }
}

/// <summary>A single GeoJSON feature from the Photon response.</summary>
internal sealed record PhotonFeature
{
    [JsonPropertyName("geometry")]
    public PhotonGeometry? Geometry { get; init; }
}

/// <summary>
/// GeoJSON point geometry. Per the GeoJSON spec <c>coordinates</c> is ordered <c>[longitude, latitude]</c> in WGS 84
/// decimal degrees — the reverse of the usual lat/lon spoken order.
/// </summary>
internal sealed record PhotonGeometry
{
    [JsonPropertyName("coordinates")]
    public IReadOnlyList<double>? Coordinates { get; init; }
}
