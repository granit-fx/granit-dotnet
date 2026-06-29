namespace Granit.Geocoding;

/// <summary>
/// Which geocoding capabilities the registered providers collectively support. Computed once from the provider
/// set so a host (or an endpoint) can branch on what is actually available — e.g. only map an autocomplete
/// endpoint when an autocomplete-capable provider is installed.
/// </summary>
/// <param name="Forward">At least one <see cref="IGeocodingProvider"/> is registered.</param>
/// <param name="Autocomplete">At least one <see cref="IAddressAutocompleteProvider"/> is registered.</param>
/// <param name="Reverse">At least one <see cref="IReverseGeocodingProvider"/> is registered.</param>
public sealed record GeocodingCapabilities(bool Forward, bool Autocomplete, bool Reverse);
