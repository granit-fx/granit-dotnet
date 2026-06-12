namespace Granit.IpGeolocation;

/// <summary>
/// Approximate geographic location, typically derived from an IP address.
/// </summary>
/// <remarks>
/// The shape is deliberately source-agnostic — a city/region/country is a location regardless of how it was
/// resolved — so a future non-IP geolocation module (e.g. address geocoding) can reuse this contract.
/// Every member is optional: a provider populates only what its data source supports (an offline
/// country-only database leaves <see cref="City"/> and the coordinates <c>null</c>).
/// </remarks>
public sealed record GeoLocation
{
    /// <summary>City name (e.g. <c>"Brussels"</c>), when the data source resolves to city granularity.</summary>
    public string? City { get; init; }

    /// <summary>Most specific subdivision — region/state/province (e.g. <c>"Brussels-Capital"</c>).</summary>
    public string? Region { get; init; }

    /// <summary>Country display name (e.g. <c>"Belgium"</c>).</summary>
    public string? Country { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. <c>"BE"</c>).</summary>
    public string? CountryCode { get; init; }

    /// <summary>Approximate latitude in decimal degrees, when available.</summary>
    public double? Latitude { get; init; }

    /// <summary>Approximate longitude in decimal degrees, when available.</summary>
    public double? Longitude { get; init; }
}
