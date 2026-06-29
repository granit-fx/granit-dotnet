using System.Text.Json.Serialization;

namespace Granit.Geocoding.Photon.Internal;

/// <summary>
/// The Photon <c>/api</c> response — a GeoJSON <c>FeatureCollection</c>. The geometry yields the coordinate and
/// the feature <c>properties</c> the match granularity and parsed components.
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

    [JsonPropertyName("properties")]
    public PhotonProperties? Properties { get; init; }
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

/// <summary>
/// Photon feature properties: <c>type</c> (<c>house</c>/<c>street</c>/<c>locality</c>/…) gives the match
/// granularity; the remaining fields are the parsed address components.
/// </summary>
internal sealed record PhotonProperties
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("street")]
    public string? Street { get; init; }

    [JsonPropertyName("housenumber")]
    public string? HouseNumber { get; init; }

    [JsonPropertyName("postcode")]
    public string? Postcode { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("countrycode")]
    public string? CountryCode { get; init; }
}
