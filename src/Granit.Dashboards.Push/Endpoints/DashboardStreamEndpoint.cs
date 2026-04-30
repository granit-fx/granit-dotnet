using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Granit.Authorization;
using Granit.Dashboards.Push.Internal;
using Granit.Dashboards.Push.Options;
using Granit.Dashboards.Rendering;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Dashboards.Push.Endpoints;

/// <summary>
/// HTTP handler for <c>GET /dashboards/{id}/stream</c> — SSE endpoint that
/// delivers per-widget envelopes published through <see cref="IWidgetPushPublisher"/>
/// to subscribed clients. ADR-043 §3.
/// </summary>
internal static class DashboardStreamEndpoint
{
    /// <summary>Permission constant — duplicated from <c>Granit.Dashboards.Endpoints.DashboardsPermissions.Instances.Read</c> so the push package doesn't depend on Endpoints just for one literal. Kept in sync by convention; the literal is also documented in CLAUDE.md.</summary>
    private const string ReadPermission = "Dashboards.Instances.Read";

    public static RouteGroupBuilder MapDashboardStreamEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/stream", StreamAsync)
            .WithName("StreamGranitDashboard")
            .WithSummary("Subscribes to live widget updates for a dashboard via Server-Sent Events.")
            .WithDescription(
                "Long-lived HTTP/1.1 (or HTTP/2) connection that stays open and emits one "
                + "SSE event per widget envelope. Event id is the widget's monotonic Sequence "
                + "number; data is the same JSON shape as DashboardRenderedWidgetResponse "
                + "minus structural fields (the frontend already has those from the seed pull). "
                + "The framework writes a comment heartbeat every HeartbeatInterval (default 15 s) "
                + "to keep reverse proxies and CDNs from closing the connection on idle. "
                + "Frontend clients open this stream after the seed pull renders, then route "
                + "envelopes whose Transport=Push (computed by the render bundle, ADR-043 §2.3) "
                + "into their TanStack cache instead of polling.")
            .RequireAuthorization(ReadPermission)
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task StreamAsync(
        Guid id,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        IWidgetPushHub hub = context.RequestServices.GetRequiredService<IWidgetPushHub>();
        IOptions<DashboardsPushOptions> options = context.RequestServices.GetRequiredService<IOptions<DashboardsPushOptions>>();
        IClock clock = context.RequestServices.GetRequiredService<IClock>();
        IPermissionChecker permissionChecker = context.RequestServices.GetRequiredService<IPermissionChecker>();
        ICurrentTenant? currentTenant = context.RequestServices.GetService<ICurrentTenant>();

        Guid? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null;

        // SSE handshake — set headers BEFORE writing anything. Once the response
        // body has started, headers are immutable.
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        // Disable nginx's reverse-proxy buffering — without this header SSE events
        // accumulate in nginx's buffer and only ship in batches.
        context.Response.Headers["X-Accel-Buffering"] = "no";

        // Channel buffers envelopes between the producer (hub) and the consumer
        // (this loop). DropOldest absorbs slow-client backpressure without
        // backpressuring the publisher; capacity is intentionally small — the
        // SSE write loop drains it on every iteration.
        var channel = Channel.CreateBounded<WidgetPushMessage>(
            new BoundedChannelOptions(capacity: 256)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

        using IDisposable subscription = hub.Subscribe(tenantId, id, channel.Writer);

        // Per-stream permission cache — populated lazily on the first envelope
        // that declares a RequiredPermission. Stays scoped to this connection
        // (permission changes mid-stream only take effect on reconnect, matching
        // the pull endpoint's snapshot-at-render-time semantics).
        ConcurrentDictionary<string, bool> permissionCache = new(StringComparer.Ordinal);

        TimeSpan heartbeat = options.Value.HeartbeatInterval;
        DateTimeOffset nextHeartbeat = clock.Now + heartbeat;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                TimeSpan readTimeout = nextHeartbeat - clock.Now;
                if (readTimeout < TimeSpan.Zero)
                {
                    readTimeout = TimeSpan.Zero;
                }
                readCts.CancelAfter(readTimeout);

                WidgetPushMessage? message = null;
                try
                {
                    message = await channel.Reader.ReadAsync(readCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Heartbeat deadline reached without a message — write a
                    // comment frame, advance the deadline, loop.
                    await context.Response.WriteAsync(": heartbeat\n\n", cancellationToken).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                    nextHeartbeat = clock.Now + heartbeat;
                    continue;
                }

                if (message is null)
                {
                    continue;
                }

                // Per-widget permission gate — mirrors the render-time check in
                // DashboardRenderer. Snapshots for widgets the subscriber lacks
                // permission to read get rewritten to Unavailable before they
                // touch the wire.
                WidgetSnapshotEnvelope effective = await WidgetPushPermissionFilter
                    .ResolveEffectiveAsync(message, permissionChecker, permissionCache, cancellationToken)
                    .ConfigureAwait(false);

                string payload = JsonSerializer.Serialize(new
                {
                    widgetId = message.WidgetInstanceId,
                    widgetType = effective.WidgetType,
                    status = effective.Status,
                    sequence = effective.Sequence,
                    emittedAt = effective.EmittedAt,
                    refreshHint = effective.RefreshHint,
                    snapshot = effective.Snapshot,
                    reasonLocalizationKey = effective.ReasonLocalizationKey,
                });

                await context.Response.WriteAsync(
                    $"event: snapshot\nid: {effective.Sequence}\ndata: {payload}\n\n",
                    cancellationToken).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

                // Reset the heartbeat deadline — we just wrote a real message.
                nextHeartbeat = clock.Now + heartbeat;
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — graceful exit. Subscription disposes via the using block.
        }
    }
}
