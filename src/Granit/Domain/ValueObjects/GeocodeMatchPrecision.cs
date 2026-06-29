namespace Granit.Domain.ValueObjects;

/// <summary>
/// Granularity of a geocoding match, derived from the provider response (e.g. Nominatim
/// <c>addresstype</c>/<c>place_rank</c>, Photon feature <c>type</c>). Drives whether a result is treated
/// as <see cref="AddressGeocodingStatus.Resolved"/> or <see cref="AddressGeocodingStatus.Approximate"/>.
/// </summary>
public enum GeocodeMatchPrecision
{
    /// <summary>Matched to an exact building / house number.</summary>
    Rooftop = 0,

    /// <summary>Matched to a street (interpolated along the road).</summary>
    Street = 1,

    /// <summary>Matched only to a locality / postcode centroid.</summary>
    Locality = 2,
}
