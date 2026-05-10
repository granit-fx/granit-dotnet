using System.Net;

namespace Granit.Http.Security.Internal;

/// <summary>Default <see cref="IDnsResolver"/> backed by <see cref="Dns"/>.</summary>
internal sealed class SystemDnsResolver : IDnsResolver
{
    public async ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct) =>
        await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
}
