namespace Granit.Geocoding;

/// <summary>
/// Suggests addresses for a partial query (typeahead), applying the configured provider order.
/// </summary>
/// <remarks>
/// Privacy-first no-op: returns an empty list when no autocomplete-capable provider is registered, and never
/// surfaces a provider failure to the caller. Not cached — typeahead queries have a low cache-hit rate and are
/// expected to be debounced by the caller.
/// </remarks>
public interface IAddressAutocompleteService
{
    /// <summary>Returns up to <paramref name="limit"/> suggestions for the partial <paramref name="query"/>.</summary>
    /// <param name="query">The partial address text typed so far.</param>
    /// <param name="limit">Maximum number of suggestions to return (clamped to a sane range).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The suggestions (empty when the query is blank or no provider is enabled); never <c>null</c>.</returns>
    Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(
        string query, int limit = 5, CancellationToken cancellationToken = default);
}
