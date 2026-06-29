using Granit.Geocoding.Diagnostics;
using Granit.Geocoding.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Geocoding.Internal;

/// <summary>
/// Default <see cref="IAddressAutocompleteService"/>: orders the autocomplete-capable providers per
/// <see cref="GranitGeocodingOptions.ProviderOrder"/> and returns the first provider's suggestions.
/// </summary>
/// <remarks>
/// No caching — typeahead queries have a low cache-hit rate and are expected to be debounced by the caller.
/// Provider exceptions are swallowed (logged with the provider name only — <strong>never the query</strong>, which
/// is partial personal data) so a failing provider degrades to the next, and an empty provider set is a silent
/// no-op. Blank or too-short queries short-circuit to an empty list before any provider is called.
/// </remarks>
internal sealed partial class DefaultAddressAutocompleteService : IAddressAutocompleteService
{
    private const int MinLimit = 1;
    private const int MaxLimit = 10;
    private const int MinQueryLength = 2;

    private readonly List<IAddressAutocompleteProvider> _providers;
    private readonly GeocodingMetrics _metrics;
    private readonly ILogger<DefaultAddressAutocompleteService> _logger;

    public DefaultAddressAutocompleteService(
        IEnumerable<IAddressAutocompleteProvider> providers,
        IOptions<GranitGeocodingOptions> options,
        GeocodingMetrics metrics,
        ILogger<DefaultAddressAutocompleteService> logger)
    {
        _metrics = metrics;
        _logger = logger;
        _providers = OrderProviders(providers, options.Value.ProviderOrder);
    }

    public async Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(
        string query, int limit = 5, CancellationToken cancellationToken = default)
    {
        if (_providers.Count == 0 || string.IsNullOrWhiteSpace(query) || query.Trim().Length < MinQueryLength)
        {
            return [];
        }

        int clampedLimit = Math.Clamp(limit, MinLimit, MaxLimit);
        string trimmed = query.Trim();

        foreach (IAddressAutocompleteProvider provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                IReadOnlyList<AddressSuggestion> suggestions =
                    await provider.SuggestAsync(trimmed, clampedLimit, cancellationToken).ConfigureAwait(false);
                if (suggestions.Count > 0)
                {
                    _metrics.RecordProviderAttempt(provider.ProviderName, "hit");
                    return suggestions;
                }

                _metrics.RecordProviderAttempt(provider.ProviderName, "miss");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Provider failures must degrade to the next provider, never bubble up.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _metrics.RecordProviderAttempt(provider.ProviderName, "error");

                // GDPR: log the provider and the exception only — never the partial query being autocompleted.
                LogProviderFailed(provider.ProviderName, ex);
            }
        }

        return [];
    }

    private static List<IAddressAutocompleteProvider> OrderProviders(
        IEnumerable<IAddressAutocompleteProvider> providers, IList<string> order)
    {
        List<IAddressAutocompleteProvider> registered = [.. providers];
        if (order.Count == 0)
        {
            return registered;
        }

        Dictionary<string, IAddressAutocompleteProvider> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (IAddressAutocompleteProvider provider in registered)
        {
            byName.TryAdd(provider.ProviderName, provider);
        }

        List<IAddressAutocompleteProvider> ordered = [];
        foreach (string name in order)
        {
            if (byName.TryGetValue(name, out IAddressAutocompleteProvider? provider))
            {
                ordered.Add(provider);
            }
        }

        return ordered;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Address autocomplete provider '{Provider}' failed; falling back to the next provider.")]
    private partial void LogProviderFailed(string provider, Exception exception);
}
