using System.Text.Json.Serialization;

namespace Granit.Geocoding.Nominatim.Internal;

/// <summary>
/// A single place from the Nominatim <c>/search</c> JSON array response. <c>lat</c> and <c>lon</c> are returned as
/// strings in WGS 84 decimal degrees.
/// </summary>
internal sealed record NominatimPlace
{
    [JsonPropertyName("lat")]
    public string? Lat { get; init; }

    [JsonPropertyName("lon")]
    public string? Lon { get; init; }
}
