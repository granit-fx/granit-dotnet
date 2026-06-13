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

    /// <summary>
    /// Privacy-detection block, present only when the API token has the privacy add-on. Absent (<c>null</c>) on
    /// the free tier, in which case the anonymising flags are left unknown.
    /// </summary>
    [JsonPropertyName("privacy")]
    public IpInfoPrivacy? Privacy { get; init; }
}

/// <summary>
/// ipinfo.io privacy-detection sub-object (paid add-on). Each flag is <c>true</c> when the IP matches that
/// category.
/// </summary>
internal sealed record IpInfoPrivacy
{
    [JsonPropertyName("vpn")]
    public bool? Vpn { get; init; }

    [JsonPropertyName("proxy")]
    public bool? Proxy { get; init; }

    [JsonPropertyName("tor")]
    public bool? Tor { get; init; }

    [JsonPropertyName("hosting")]
    public bool? Hosting { get; init; }
}
