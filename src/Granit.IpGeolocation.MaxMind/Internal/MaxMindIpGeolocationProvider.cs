using System.Net;
using Granit.IpGeolocation.MaxMind.Options;
using Microsoft.Extensions.Options;

namespace Granit.IpGeolocation.MaxMind.Internal;

/// <summary>
/// Offline <see cref="IIpGeolocationProvider"/> backed by a MaxMind/DB-IP <c>.mmdb</c> database. Lookups are
/// in-memory and synchronous, so resolution stays on-premise (no third-party call).
/// </summary>
internal sealed class MaxMindIpGeolocationProvider(
    MaxMindDatabaseProvider database,
    IOptions<MaxMindIpGeolocationOptions> options) : IIpGeolocationProvider
{
    public string ProviderName => options.Value.ProviderName;

    public Task<GeoLocation?> ResolveAsync(string ipAddress, CancellationToken cancellationToken = default) =>
        Task.FromResult(IPAddress.TryParse(ipAddress, out IPAddress? address) ? database.Lookup(address) : null);
}
