using Granit.Domain.ValueObjects;

namespace Granit.Geocoding;

/// <summary>
/// A single address-autocomplete suggestion: a display label plus the structured components a UI uses to fill a
/// form when the user picks it.
/// </summary>
/// <param name="Label">Human-readable one-line label for the typeahead list (e.g. <c>"Rue de la Loi 16, 1000 Brussels, BE"</c>).</param>
/// <param name="Address">The structured postal components of the suggestion.</param>
/// <param name="Coordinate">The suggestion's coordinate, or <c>null</c> when the provider returned none.</param>
/// <param name="Precision">Granularity of the suggestion (rooftop / street / locality), or <c>null</c> when unknown.</param>
public sealed record AddressSuggestion(
    string Label,
    PostalAddress Address,
    GeoCoordinate? Coordinate,
    GeocodeMatchPrecision? Precision);
