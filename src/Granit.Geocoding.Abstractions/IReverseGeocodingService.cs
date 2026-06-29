using Granit.Domain.ValueObjects;

namespace Granit.Geocoding;

/// <summary>
/// Reverse-geocodes a <see cref="GeoCoordinate"/> to an approximate <see cref="ReverseGeocodingResult"/>, applying
/// the configured provider fallback order and result caching.
/// </summary>
/// <remarks>
/// Privacy-first no-op: returns <c>null</c> when no reverse-capable provider is registered, and never surfaces a
/// provider failure to the caller. The coordinate (personal data) is never written to a log, trace tag, or cache key.
/// </remarks>
public interface IReverseGeocodingService
{
    /// <summary>Resolves <paramref name="coordinate"/> to the nearest postal address.</summary>
    /// <param name="coordinate">The coordinate to reverse-geocode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The resolved result, or <c>null</c> when no reverse-capable provider is enabled or every provider in the
    /// fallback chain failed or had no match. <strong>Never throws</strong> for these cases.
    /// </returns>
    Task<ReverseGeocodingResult?> ReverseAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default);
}
