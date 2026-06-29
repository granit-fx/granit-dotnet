using System.Text.Json.Serialization;

namespace Granit.Geocoding.Nominatim.Internal;

/// <summary>
/// A single place from the Nominatim <c>/search</c> JSON array response (<c>format=jsonv2</c>,
/// <c>addressdetails=1</c>). <c>lat</c>/<c>lon</c> are WGS 84 decimal degrees as strings; <c>addresstype</c>
/// and <c>place_rank</c> give the match granularity; the nested <c>address</c> object carries the parsed components.
/// </summary>
internal sealed record NominatimPlace
{
    [JsonPropertyName("lat")]
    public string? Lat { get; init; }

    [JsonPropertyName("lon")]
    public string? Lon { get; init; }

    [JsonPropertyName("addresstype")]
    public string? AddressType { get; init; }

    [JsonPropertyName("place_rank")]
    public int PlaceRank { get; init; }

    [JsonPropertyName("address")]
    public NominatimAddress? Address { get; init; }
}

/// <summary>Parsed address components from Nominatim's <c>addressdetails=1</c> payload.</summary>
internal sealed record NominatimAddress
{
    [JsonPropertyName("house_number")]
    public string? HouseNumber { get; init; }

    [JsonPropertyName("road")]
    public string? Road { get; init; }

    [JsonPropertyName("postcode")]
    public string? Postcode { get; init; }

    // Nominatim names the locality differently by place type; the first non-empty one wins.
    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("town")]
    public string? Town { get; init; }

    [JsonPropertyName("village")]
    public string? Village { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; init; }

    /// <summary>The locality from whichever of city/town/village the response carries.</summary>
    public string? Locality => City ?? Town ?? Village;
}
