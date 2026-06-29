using Granit.Domain.ValueObjects;

namespace Granit.Geocoding;

/// <summary>
/// The outcome of a successful forward-geocoding lookup: the resolved <see cref="GeoCoordinate"/> plus the
/// match granularity and the provider-parsed address components.
/// </summary>
/// <remarks>
/// The components are used downstream to enrich the stored geocoding (<c>HouseNumber</c>) and to cross-check
/// the result against the submitted address (a mismatched <c>PostalCode</c>/<c>CountryCode</c> flags a
/// likely typo). They are best-effort: a provider that returns only a coordinate leaves them <c>null</c>.
/// </remarks>
/// <param name="Coordinate">The resolved coordinate.</param>
/// <param name="Precision">Granularity of the match (rooftop / street / locality).</param>
/// <param name="HouseNumber">House / building number parsed by the provider, or <c>null</c>.</param>
/// <param name="PostalCode">Postal code parsed by the provider, or <c>null</c>.</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 country code parsed by the provider, or <c>null</c>.</param>
public sealed record GeocodingResult(
    GeoCoordinate Coordinate,
    GeocodeMatchPrecision Precision,
    string? HouseNumber = null,
    string? PostalCode = null,
    string? CountryCode = null);
