namespace Granit.Geocoding.Endpoints.Dtos;

/// <summary>
/// A single address-autocomplete suggestion, flattened for the wire.
/// </summary>
/// <param name="Label">Human-readable one-line label for the typeahead list.</param>
/// <param name="Street">Street line, or <c>null</c>.</param>
/// <param name="PostalCode">Postal code, or <c>null</c>.</param>
/// <param name="Locality">Locality (city/town).</param>
/// <param name="Country">Country (ISO 3166-1 alpha-2 where known).</param>
/// <param name="Latitude">Latitude, or <c>null</c> when the provider returned no coordinate.</param>
/// <param name="Longitude">Longitude, or <c>null</c> when the provider returned no coordinate.</param>
public sealed record GeocodingSuggestionResponse(
    string Label,
    string? Street,
    string? PostalCode,
    string Locality,
    string Country,
    double? Latitude,
    double? Longitude);
