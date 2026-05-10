using System.Net;
using System.Net.Sockets;
using Granit.Http.Security.Internal;

namespace Granit.Http.Security.Tests;

/// <summary>Test double for <see cref="IDnsResolver"/>.</summary>
internal sealed class FakeDnsResolver(Func<string, CancellationToken, ValueTask<IPAddress[]>> resolve) : IDnsResolver
{
    public int CallCount { get; private set; }

    public ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct)
    {
        CallCount++;
        return resolve(host, ct);
    }

    public static FakeDnsResolver Returning(params string[] addresses) =>
        new((_, _) => ValueTask.FromResult<IPAddress[]>([.. addresses.Select(IPAddress.Parse)]));

    public static FakeDnsResolver Throws(SocketError code = SocketError.HostNotFound) =>
        new((_, _) => throw new SocketException((int)code));

    public static FakeDnsResolver Hangs() =>
        new(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            return [];
        });

    public static FakeDnsResolver Empty() =>
        new((_, _) => ValueTask.FromResult<IPAddress[]>([]));
}
