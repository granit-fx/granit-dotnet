namespace Granit.Geocoding;

/// <summary>
/// Low-level address-autocomplete (typeahead) lookup — an optional provider capability.
/// </summary>
/// <remarks>
/// A provider package implements this only when its backend is built for typeahead (e.g. Photon). A pure
/// forward/reverse geocoder (e.g. Nominatim, whose usage policy discourages per-keystroke queries) does not.
/// Registering the same provider instance for every capability it supports keeps one shared rate-limit throttle.
/// </remarks>
public interface IAddressAutocompleteProvider
{
    /// <summary>
    /// Stable identifier used to order this provider in the geocoding <c>ProviderOrder</c> (e.g. <c>"Photon"</c>).
    /// </summary>
    string ProviderName { get; }

    /// <summary>Returns up to <paramref name="limit"/> suggestions for the partial <paramref name="query"/>.</summary>
    /// <param name="query">The partial address text typed so far.</param>
    /// <param name="limit">Maximum number of suggestions to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The suggestions (possibly empty); never <c>null</c>.</returns>
    Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(
        string query, int limit, CancellationToken cancellationToken = default);
}
