using Granit.Domain.ValueObjects;

namespace Granit.IpGeolocation;

/// <summary>
/// Approximate geographic location, typically derived from an IP address.
/// </summary>
/// <remarks>
/// The shape is deliberately source-agnostic — a city/region/country is a location regardless of how it was
/// resolved — so a future non-IP geolocation module (e.g. address geocoding) can reuse this contract.
/// Every member is optional: a provider populates only what its data source supports (an offline
/// country-only database leaves <see cref="City"/> and the <see cref="Coordinate"/> <c>null</c>).
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

    /// <summary>
    /// Approximate WGS 84 coordinate, when the source resolves to one. Latitude and longitude are a single unit —
    /// a source provides both or neither — so they are modelled as one range-validated value object rather than two
    /// independently-nullable doubles.
    /// </summary>
    public GeoCoordinate? Coordinate { get; init; }

    /// <summary>
    /// Radius in kilometres within which the true position lies with ~67% confidence, when the source reports
    /// it. A large radius means a coarse, low-confidence fix (mobile-carrier NAT, satellite, sparse data) —
    /// consumers should not treat distance computed against it as precise. <c>null</c> when unknown.
    /// </summary>
    public int? AccuracyRadiusKm { get; init; }

    /// <summary>
    /// Whether the IP is a known anonymising proxy (e.g. Tor / open proxy), when the source classifies it.
    /// <c>null</c> when the source does not provide anonymising-IP data.
    /// </summary>
    public bool? IsAnonymousProxy { get; init; }

    /// <summary>
    /// Whether the IP belongs to a hosting/datacenter provider, when the source classifies it. Datacenter
    /// egress (cloud, server-side fetch) frequently geolocates far from the human behind it. <c>null</c> when
    /// the source does not provide it.
    /// </summary>
    public bool? IsHostingProvider { get; init; }

    /// <summary>
    /// Whether the IP is a known VPN exit node, when the source classifies it. <c>null</c> when the source
    /// does not provide it.
    /// </summary>
    public bool? IsVpn { get; init; }
}
