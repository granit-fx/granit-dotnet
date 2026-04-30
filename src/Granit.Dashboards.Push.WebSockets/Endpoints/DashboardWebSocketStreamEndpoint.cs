using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Granit.Authorization;
using Granit.Dashboards.Push.Internal;
using Granit.Dashboards.Push.WebSockets.Options;
using Granit.Dashboards.Rendering;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Dashboards.Push.WebSockets.Endpoints;

/// <summary>
/// HTTP handler for <c>GET /dashboards/{id}/stream-ws</c> — WebSocket sibling
/// of <c>GET /dashboards/{id}/stream</c> (SSE). Reuses the same
/// <see cref="IWidgetPushHub"/> + <see cref="WidgetPushPermissionFilter"/>
/// from <c>Granit.Dashboards.Push</c>. ADR-043 §1, §3.
/// </summary>
internal static class DashboardWebSocketStreamEndpoint
{
    /// <summary>Permission constant — duplicated from <c>Granit.Dashboards.Endpoints.DashboardsPermissions.Instances.Read</c> so the WebSocket package doesn't depend on Endpoints just for one literal. Kept in sync by convention; the literal is also documented in CLAUDE.md.</summary>
    private const string ReadPermission = "Dashboards.Instances.Read";

    public static RouteGroupBuilder MapDashboardWebSocketStreamEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/stream-ws", StreamAsync)
            .WithName("StreamGranitDashboardWebSocket")
            .WithSummary("Subscribes to live widget updates for a dashboard via WebSocket.")
            .WithDescription(
                "Long-lived WebSocket connection emitting one JSON frame per envelope. "
                + "Frame shape: { type: \"snapshot\" | \"resume-failed\", id?: long, data?: { ... } } "
                + "where data mirrors the SSE payload field-for-field. Optional opening frame "
                + "from the client of shape { lastEventId: N } drives the per-stream replay window "
                + "(WebSockets don't support the SSE Last-Event-ID header post-upgrade, so the "
                + "framework reads it from the first inbound text frame within OpeningFrameTimeout). "
                + "The opening frame is optional — clients that omit it get a fresh subscription.")
            .RequireAuthorization(ReadPermission)
            .Produces(StatusCodes.Status101SwitchingProtocols)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task StreamAsync(
        Guid id,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(
                "Endpoint requires a WebSocket upgrade.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        IWidgetPushHub hub = context.RequestServices.GetRequiredService<IWidgetPushHub>();
        IOptions<DashboardsPushWebSocketsOptions> options = context.RequestServices.GetRequiredService<IOptions<DashboardsPushWebSocketsOptions>>();
        IPermissionChecker permissionChecker = context.RequestServices.GetRequiredService<IPermissionChecker>();
        ICurrentTenant? currentTenant = context.RequestServices.GetService<ICurrentTenant>();

        Guid? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null;

        using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

        long? lastEventId = await TryReadLastEventIdAsync(
            webSocket, options.Value.OpeningFrameTimeout, cancellationToken).ConfigureAwait(false);

        var channel = Channel.CreateBounded<WidgetPushMessage>(
            new BoundedChannelOptions(capacity: 256)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

        SubscriptionResult subscriptionResult = hub.Subscribe(tenantId, id, lastEventId, channel.Writer);
        using IDisposable subscription = subscriptionResult.Handle;

        // Per-stream permission cache — same semantics as the SSE handler.
        ConcurrentDictionary<string, bool> permissionCache = new(StringComparer.Ordinal);

        try
        {
            if (subscriptionResult.ResumeFailed)
            {
                await SendFrameAsync(webSocket, """{"type":"resume-failed"}""", cancellationToken)
                    .ConfigureAwait(false);
            }

            // Replay phase — flush buffered envelopes the client missed during
            // its disconnect window. Atomic with subscribe (ADR-043 §5).
            foreach (WidgetPushMessage replay in subscriptionResult.Replay)
            {
                await SendEnvelopeAsync(webSocket, replay, permissionChecker, permissionCache, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Live loop — drains the channel until cancellation or the client
            // sends a Close frame (which surfaces as OperationCanceledException
            // on ReadAsync via the request-aborted token).
            while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                WidgetPushMessage message = await channel.Reader
                    .ReadAsync(cancellationToken)
                    .ConfigureAwait(false);

                await SendEnvelopeAsync(webSocket, message, permissionChecker, permissionCache, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — graceful exit.
        }
        catch (WebSocketException)
        {
            // Connection dropped mid-write — graceful exit, subscription disposes via using.
        }

        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                statusDescription: "stream closed",
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the optional opening text frame, parses <c>{ "lastEventId": N }</c>,
    /// and returns the cursor (or <see langword="null"/> when the client sent
    /// no frame within <paramref name="timeout"/> or the JSON didn't carry the
    /// field). Bad / malformed openings fall back to fresh subscription.
    /// </summary>
    private static async Task<long?> TryReadLastEventIdAsync(
        WebSocket webSocket,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(2048);
        try
        {
            WebSocketReceiveResult receive;
            try
            {
                receive = await webSocket
                    .ReceiveAsync(new ArraySegment<byte>(buffer), timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;                                                 // timeout — no opening frame, treat as fresh
            }

            if (receive.MessageType != WebSocketMessageType.Text || receive.Count == 0)
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(buffer.AsMemory(0, receive.Count));
                if (doc.RootElement.TryGetProperty("lastEventId", out JsonElement value)
                    && value.ValueKind == JsonValueKind.Number
                    && value.TryGetInt64(out long parsed))
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Bad JSON — fall back to fresh subscription. Never break the stream.
            }

            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task SendEnvelopeAsync(
        WebSocket webSocket,
        WidgetPushMessage message,
        IPermissionChecker permissionChecker,
        ConcurrentDictionary<string, bool> permissionCache,
        CancellationToken cancellationToken)
    {
        WidgetSnapshotEnvelope effective = await WidgetPushPermissionFilter
            .ResolveEffectiveAsync(message, permissionChecker, permissionCache, cancellationToken)
            .ConfigureAwait(false);

        string frame = JsonSerializer.Serialize(new
        {
            type = "snapshot",
            id = message.StreamCursor,
            data = new
            {
                widgetId = message.WidgetInstanceId,
                widgetType = effective.WidgetType,
                status = effective.Status,
                sequence = effective.Sequence,
                emittedAt = effective.EmittedAt,
                refreshHint = effective.RefreshHint,
                snapshot = effective.Snapshot,
                reasonLocalizationKey = effective.ReasonLocalizationKey,
            },
        });

        await SendFrameAsync(webSocket, frame, cancellationToken).ConfigureAwait(false);
    }

    private static Task SendFrameAsync(
        WebSocket webSocket,
        string json,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        return webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }
}
