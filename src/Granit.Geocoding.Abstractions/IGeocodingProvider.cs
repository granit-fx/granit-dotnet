using Granit.Domain.ValueObjects;

namespace Granit.Geocoding;

/// <summary>
/// Low-level forward-geocoding lookup implemented by each provider package (e.g. <c>Granit.Geocoding.Nominatim</c>).
/// </summary>
/// <remarks>
/// Implementations are registered with <c>AddSingleton&lt;IGeocodingProvider, ...&gt;()</c> so multiple providers
/// coexist; <see cref="IGeocodingService"/> orders and chains them. Implementations should be thread-safe.
/// </remarks>
public interface IGeocodingProvider
{
    /// <summary>
    /// Stable identifier used to order this provider in the geocoding <c>ProviderOrder</c> (e.g. <c>"Nominatim"</c>).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Looks up <paramref name="address"/> in this provider's data source.
    /// </summary>
    /// <param name="address">The address to geocode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved coordinate, or <c>null</c> when this provider has no match for the address.</returns>
    Task<GeoCoordinate?> ResolveAsync(PostalAddress address, CancellationToken cancellationToken = default);
}
