using Granit.Domain.ValueObjects;

namespace Granit.Geocoding;

/// <summary>
/// Forward-geocodes a <see cref="PostalAddress"/> to an approximate <see cref="GeoCoordinate"/>, applying the
/// configured provider fallback order and result caching.
/// </summary>
/// <remarks>
/// This is the primary entry point consumers inject. It is a privacy-first no-op when no provider is registered or
/// enabled: it returns <c>null</c> rather than throwing, and never surfaces a provider failure to the caller.
/// </remarks>
public interface IGeocodingService
{
    /// <summary>
    /// Resolves <paramref name="address"/> to an approximate coordinate.
    /// </summary>
    /// <param name="address">The address to geocode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The resolved coordinate, or <c>null</c> when the address is not geocodable, no provider is enabled, or every
    /// provider in the fallback chain failed or had no match. <strong>Never throws</strong> for these cases.
    /// </returns>
    Task<GeoCoordinate?> GeocodeAsync(PostalAddress address, CancellationToken cancellationToken = default);
}
