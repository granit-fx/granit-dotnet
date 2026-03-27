using System.Net;
using System.Net.Sockets;

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
        IPHostEntry entry = await Dns.GetHostEntryAsync(
            context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);

        // Validate ALL resolved IPs before connecting — a multi-homed host might mix
        // public and private addresses.
        foreach (IPAddress ip in entry.AddressList)
        {
            if (WebhookSsrfGuard.IsBlockedIpAddress(ip))
            {
                throw new HttpRequestException(
                    $"Webhook delivery blocked: '{context.DnsEndPoint.Host}' resolved to " +
                    $"blocked address {ip}. Private, loopback, and link-local addresses " +
                    "are not permitted for webhook target URLs (SSRF protection).");
            }
        }

        // Connect to the first available address.
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        try
        {
            await socket.ConnectAsync(entry.AddressList, context.DnsEndPoint.Port, cancellationToken)
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
