using System.Net;
using System.Net.Sockets;
using Granit.Http.Security;

namespace Granit.Webhooks.Internal;

/// <summary>
/// <see cref="SocketsHttpHandler.ConnectCallback"/> implementation that validates resolved
/// IP addresses against the SSRF blocklist before establishing a TCP connection.
/// Prevents DNS rebinding attacks where a hostname resolves to an internal IP at delivery time.
/// </summary>
internal static class WebhookSsrfConnectCallback
{
    internal static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Forward-only A/AAAA lookup — never GetHostEntryAsync, which also triggers a reverse-PTR
        // lookup (slow, can hang on broken reverse zones, leaks internal hostnames over DNS).
        IPAddress[] addresses = await Dns
            .GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken)
            .ConfigureAwait(false);

        if (addresses.Length == 0)
        {
            throw new HttpRequestException(
                $"Webhook delivery blocked: DNS returned no addresses for " +
                $"'{context.DnsEndPoint.Host}'.");
        }

        // Validate ALL resolved IPs before connecting — a multi-homed host might mix
        // public and private addresses.
        IPAddress? blocked = Array.Find(addresses, PrivateNetworkClassifier.IsBlocked);
        if (blocked is not null)
        {
            throw new HttpRequestException(
                $"Webhook delivery blocked: '{context.DnsEndPoint.Host}' resolved to " +
                $"blocked address {blocked}. Private, loopback, and link-local addresses " +
                "are not permitted for webhook target URLs (SSRF protection).");
        }

        // Connect to one of the validated addresses (no further DNS).
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken)
                .ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (Exception)
        {
            socket.Dispose();
            throw;
        }
    }
}
