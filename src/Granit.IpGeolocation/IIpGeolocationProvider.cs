namespace Granit.IpGeolocation;

/// <summary>
/// Low-level IP-to-location lookup implemented by each provider package
/// (e.g. <c>Granit.IpGeolocation.MaxMind</c>, <c>Granit.IpGeolocation.IpApi</c>).
/// </summary>
/// <remarks>
/// Implementations are registered with <c>AddSingleton&lt;IIpGeolocationProvider, ...&gt;()</c> so multiple
/// providers coexist; <see cref="IIpGeolocationResolver"/> orders and chains them. Implementations should be
/// thread-safe. They are only ever called with a parsed, public IP address — the resolver short-circuits
/// private/loopback/unparseable addresses — so providers need not re-validate for those cases.
/// </remarks>
public interface IIpGeolocationProvider
{
    /// <summary>
    /// Stable identifier used to order this provider in
    /// <see cref="Options.GranitIpGeolocationOptions.ProviderOrder"/> (e.g. <c>"MaxMind"</c>, <c>"IpApi"</c>).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Looks up <paramref name="ipAddress"/> in this provider's data source.
    /// </summary>
    /// <param name="ipAddress">A public IPv4 or IPv6 address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved location, or <c>null</c> when this provider has no data for the address.</returns>
    Task<GeoLocation?> ResolveAsync(string ipAddress, CancellationToken cancellationToken = default);
}
