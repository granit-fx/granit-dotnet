using Granit.Domain.ValueObjects;

namespace Granit.Geocoding.Endpoints.Dtos;

/// <summary>The postal address nearest a reverse-geocoded coordinate.</summary>
/// <param name="Street">Street line, or <c>null</c>.</param>
/// <param name="PostalCode">Postal code, or <c>null</c>.</param>
/// <param name="Locality">Locality (city/town).</param>
/// <param name="Country">Country (ISO 3166-1 alpha-2).</param>
/// <param name="Precision">Match granularity (rooftop / street / locality).</param>
public sealed record GeocodingReverseResponse(
    string? Street,
    string? PostalCode,
    string Locality,
    string Country,
    GeocodeMatchPrecision Precision);
