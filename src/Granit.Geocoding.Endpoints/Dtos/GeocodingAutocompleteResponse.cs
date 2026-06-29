namespace Granit.Geocoding.Endpoints.Dtos;

/// <summary>The address-autocomplete suggestions for a partial query.</summary>
/// <param name="Suggestions">The suggestions, best match first (possibly empty).</param>
public sealed record GeocodingAutocompleteResponse(IReadOnlyList<GeocodingSuggestionResponse> Suggestions);
