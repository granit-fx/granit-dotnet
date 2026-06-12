using System.Text.Json.Serialization;

namespace Granit.IpGeolocation.IpInfo.Internal;

/// <summary>
/// Subset of the ipinfo.io JSON response consumed by the provider. <c>country</c> is an ISO alpha-2 code;
/// <c>loc</c> is a <c>"lat,lng"</c> string.
/// </summary>
internal sealed record IpInfoResponse
{
    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("region")]
    public string? Region { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("loc")]
    public string? Loc { get; init; }

    [JsonPropertyName("bogon")]
    public bool? Bogon { get; init; }
}
