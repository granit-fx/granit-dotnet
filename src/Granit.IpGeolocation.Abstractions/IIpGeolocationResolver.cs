namespace Granit.IpGeolocation;

/// <summary>
/// Resolves an IP address to an approximate <see cref="GeoLocation"/>, applying the configured provider
/// fallback order and result caching.
/// </summary>
/// <remarks>
/// This is the primary entry point consumers inject. It is a privacy-first no-op when no provider is
/// registered or enabled: it returns <c>null</c> rather than throwing, and short-circuits private,
/// loopback, and unparseable addresses without ever contacting a provider.
/// </remarks>
public interface IIpGeolocationResolver
{
    /// <summary>
    /// Resolves <paramref name="ipAddress"/> to an approximate location.
    /// </summary>
    /// <param name="ipAddress">The client IP address (IPv4 or IPv6). May be <c>null</c> or empty.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The resolved location, or <c>null</c> when the address is absent/private/unparseable, no provider is
    /// enabled, or every provider in the fallback chain failed or had no data. Never throws for these cases.
    /// </returns>
    Task<GeoLocation?> ResolveAsync(string? ipAddress, CancellationToken cancellationToken = default);
}
