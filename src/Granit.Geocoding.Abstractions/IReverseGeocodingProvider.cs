using Granit.Domain.ValueObjects;

namespace Granit.Geocoding;

/// <summary>
/// Low-level reverse-geocoding lookup (coordinate → address) — an optional provider capability.
/// </summary>
/// <remarks>
/// A provider package implements this <em>in addition to</em> <see cref="IGeocodingProvider"/> when its backend
/// supports reverse lookups (e.g. Nominatim / Photon <c>/reverse</c>). Registering the same provider instance for
/// both interfaces keeps a single shared rate-limit throttle across forward and reverse calls. Implementations
/// should be thread-safe.
/// </remarks>
public interface IReverseGeocodingProvider
{
    /// <summary>
    /// Stable identifier used to order this provider in the geocoding <c>ProviderOrder</c> (e.g. <c>"Nominatim"</c>).
    /// </summary>
    string ProviderName { get; }

    /// <summary>Looks up the address nearest <paramref name="coordinate"/> in this provider's data source.</summary>
    /// <param name="coordinate">The coordinate to reverse-geocode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved result, or <c>null</c> when this provider has no match.</returns>
    Task<ReverseGeocodingResult?> ReverseAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default);
}
