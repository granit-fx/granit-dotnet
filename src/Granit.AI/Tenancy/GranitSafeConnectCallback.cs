using System.Net;
using System.Net.Sockets;

namespace Granit.AI.Tenancy;

/// <summary>
/// <see cref="SocketsHttpHandler.ConnectCallback"/> that revalidates the resolved IP address of
/// each outbound connection against the provider's <see cref="AIEndpointPolicy"/>.
/// </summary>
/// <remarks>
/// <para>
/// Defeats DNS rebinding. At endpoint-write time the validator
/// inspects either the hostname or the IP literal; an attacker who controls authoritative DNS
/// can return a public IP at validation time and a private/metadata IP at HTTP-call time.
/// Attaching this delegate to the <see cref="HttpClient"/> via a
/// <see cref="SocketsHttpHandler"/> forces the validator to re-check the address that the
/// handler is about to connect to.
/// </para>
/// <para>
/// Usage (in each provider's <c>AddHttpClient</c> wiring):
/// <code>
/// services
///     .AddHttpClient(name, client => client.Timeout = Timeout.InfiniteTimeSpan)
///     .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
///     {
///         AllowAutoRedirect = false,
///         ConnectCallback = GranitSafeConnectCallback.Create(policy),
///     });
/// </code>
/// </para>
/// </remarks>
public static class GranitSafeConnectCallback
{
    /// <summary>
    /// Builds the connect-time callback enforcing <paramref name="policy"/>.
    /// </summary>
    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> Create(
        AIEndpointPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return (ctx, ct) => ConnectAsync(ctx, policy, ct);
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        AIEndpointPolicy policy,
        CancellationToken cancellationToken)
    {
        DnsEndPoint endpoint = context.DnsEndPoint;
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);

        // Pick the first address that passes the policy.
        IPAddress? acceptable = null;
        foreach (IPAddress ip in addresses)
        {
            if (AIEndpointValidator.CheckIpRanges(ip, policy) is null)
            {
                acceptable = ip;
                break;
            }
        }

        if (acceptable is null)
        {
            throw new HttpRequestException(
                $"AI endpoint connect blocked: host '{endpoint.Host}' resolves only to addresses " +
                "rejected by the provider's AIEndpointPolicy (anti-SSRF). Verify the endpoint and DNS records.");
        }

        Socket socket = new(acceptable.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        try
        {
            await socket.ConnectAsync(acceptable, endpoint.Port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
