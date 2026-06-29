using Granit.Domain.ValueObjects;
using Granit.Geocoding;

namespace Granit.OpenApi.Generator;

/// <summary>
/// No-op geocoding provider registered only by the contract generator so the capability-gated geocoding
/// endpoints (<c>/autocomplete</c>, <c>/reverse</c>) are mapped and appear in the emitted OpenAPI document.
/// </summary>
/// <remarks>
/// The generator builds a provider-less module graph; geocoding endpoints are mapped only when a matching
/// provider is registered (<see cref="GeocodingCapabilities"/>), so without this stub the geocoding contract
/// would ship with an empty <c>paths</c> object. The stub never runs at request time — the generator only
/// probes minimal-API bindings — so every method returns the empty result.
/// </remarks>
internal sealed class GeneratorStubGeocodingProvider
    : IGeocodingProvider, IReverseGeocodingProvider, IAddressAutocompleteProvider
{
    public string ProviderName => "GeneratorStub";

    public Task<GeocodingResult?> ResolveAsync(PostalAddress address, CancellationToken cancellationToken = default) =>
        Task.FromResult<GeocodingResult?>(null);

    public Task<ReverseGeocodingResult?> ReverseAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default) =>
        Task.FromResult<ReverseGeocodingResult?>(null);

    public Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(
        string query, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AddressSuggestion>>([]);
}
