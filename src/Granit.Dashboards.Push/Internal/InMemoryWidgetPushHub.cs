using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Push.Options;
using Granit.Dashboards.Rendering;
using Microsoft.Extensions.Options;

namespace Granit.Dashboards.Push.Internal;

/// <summary>
/// In-process implementation of <see cref="IWidgetPushHub"/>. Producers call
/// <see cref="PublishSnapshotAsync"/> / <see cref="PublishUnavailableAsync"/>;
/// the SSE endpoint subscribes via <see cref="Subscribe"/>; the hub fans out
/// inside the same process and keeps a per-stream ring buffer for
/// <c>Last-Event-ID</c> resume (ADR-043 §5).
/// </summary>
/// <remarks>
/// <para>
/// Single-host deployments are the v1 target. Multi-host deployments DI-replace
/// this with a Redis / Wolverine-backed adapter sharing the same
/// <see cref="IWidgetPushHub"/> contract — see ADR-043 §4.
/// </para>
/// <para>
/// Slow subscribers are dropped silently (<c>TryWrite</c>) instead of
/// backpressuring the publisher. A subscriber whose channel is bounded and full
/// loses the in-flight message; the SSE handler shapes its channel as bounded
/// with <see cref="BoundedChannelFullMode.DropOldest"/> on the rendering side,
/// so practical loss only happens when the client has disconnected and is
/// about to be unregistered.
/// </para>
/// <para>
/// Per-stream state (cursor + ring + subscriber set) is guarded by a
/// <see cref="System.Threading.Lock"/> so dispatch (cursor allocation +
/// ring append + fan-out) and subscribe-with-replay are mutually atomic —
/// no envelope can land in both the replay snapshot and the live channel.
/// </para>
/// </remarks>
internal sealed class InMemoryWidgetPushHub(
    WidgetPushSequenceAllocator sequenceAllocator,
    IOptions<DashboardsPushOptions> options) : IWidgetPushHub
{
    private readonly WidgetPushSequenceAllocator _sequenceAllocator = sequenceAllocator;
    private readonly int _ringCapacity = options.Value.RingBufferCapacity;
    private readonly ConcurrentDictionary<StreamKey, StreamState> _streams = new();

    /// <inheritdoc/>
    public Task PublishSnapshotAsync(
        Guid? tenantId,
        Guid dashboardId,
        Guid widgetInstanceId,
        string widgetType,
        string? requiredPermission,
        JsonElement snapshot,
        DateTimeOffset emittedAt,
        RefreshHint refreshHint,
        CancellationToken cancellationToken = default)
    {
        long sequence = _sequenceAllocator.Next(tenantId, widgetInstanceId);
        var envelope = WidgetSnapshotEnvelope.ForSnapshot(
            widgetType, snapshot, sequence, emittedAt, refreshHint);

        Dispatch(tenantId, dashboardId, widgetInstanceId, requiredPermission, envelope);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task PublishUnavailableAsync(
        Guid? tenantId,
        Guid dashboardId,
        Guid widgetInstanceId,
        string widgetType,
        string? requiredPermission,
        DateTimeOffset emittedAt,
        RefreshHint refreshHint,
        string reasonLocalizationKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonLocalizationKey);

        long sequence = _sequenceAllocator.Next(tenantId, widgetInstanceId);
        var envelope = WidgetSnapshotEnvelope.Unavailable(
            widgetType, sequence, emittedAt, refreshHint, reasonLocalizationKey);

        Dispatch(tenantId, dashboardId, widgetInstanceId, requiredPermission, envelope);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public SubscriptionResult Subscribe(
        Guid? tenantId,
        Guid dashboardId,
        long? lastEventId,
        ChannelWriter<WidgetPushMessage> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var key = new StreamKey(tenantId, dashboardId);
        StreamState state = _streams.GetOrAdd(key, _ => new StreamState(_ringCapacity));

#pragma warning disable GRSEC002 // In-process subscription id, opaque, never persisted — sequential UUID would buy nothing.
        var subscriptionId = Guid.NewGuid();
#pragma warning restore GRSEC002

        lock (state.Mutex)
        {
            state.Subscribers[subscriptionId] = writer;

            if (lastEventId is null)
            {
                return new SubscriptionResult(
                    ResumeFailed: false,
                    ServerCursor: state.Cursor,
                    Replay: [],
                    Handle: new SubscriptionHandle(this, key, subscriptionId));
            }

            long requestedFrom = lastEventId.Value + 1;

            // Resume failed when the client is behind the oldest ring entry —
            // we can't replay envelopes that have aged out.
            if (state.Ring.Count > 0 && state.Ring.Peek().StreamCursor > requestedFrom)
            {
                return new SubscriptionResult(
                    ResumeFailed: true,
                    ServerCursor: state.Cursor,
                    Replay: [],
                    Handle: new SubscriptionHandle(this, key, subscriptionId));
            }

            // Caught up or in-range — collect everything strictly newer than the
            // client's last seen cursor. Empty list when fully caught up.
            WidgetPushMessage[] replay = [.. state.Ring.Where(m => m.StreamCursor >= requestedFrom)];

            return new SubscriptionResult(
                ResumeFailed: false,
                ServerCursor: state.Cursor,
                Replay: replay,
                Handle: new SubscriptionHandle(this, key, subscriptionId));
        }
    }

    private void Dispatch(
        Guid? tenantId,
        Guid dashboardId,
        Guid widgetInstanceId,
        string? requiredPermission,
        WidgetSnapshotEnvelope envelope)
    {
        var key = new StreamKey(tenantId, dashboardId);
        StreamState state = _streams.GetOrAdd(key, _ => new StreamState(_ringCapacity));

        WidgetPushMessage message;
        ChannelWriter<WidgetPushMessage>[] writers;

        lock (state.Mutex)
        {
            long cursor = ++state.Cursor;
            message = new WidgetPushMessage(
                tenantId, dashboardId, widgetInstanceId, requiredPermission, cursor, envelope);

            // Append to ring + evict oldest when over capacity. Bounded under
            // the same lock as Subscribe so no race between an envelope landing
            // in the ring and a new subscriber's replay snapshot.
            state.Ring.Enqueue(message);
            while (state.Ring.Count > _ringCapacity)
            {
                state.Ring.Dequeue();
            }

            // Snapshot the writer list under the lock; TryWrite outside to keep
            // the critical section as short as possible.
            writers = [.. state.Subscribers.Values];
        }

        foreach (ChannelWriter<WidgetPushMessage> writer in writers)
        {
            // TryWrite is non-blocking. Slow / closed subscribers silently drop —
            // the SSE handler unregisters on disconnect via the SubscriptionHandle.
            writer.TryWrite(message);
        }
    }

    private void Unregister(StreamKey key, Guid subscriptionId)
    {
        if (!_streams.TryGetValue(key, out StreamState? state))
        {
            return;
        }

        lock (state.Mutex)
        {
            state.Subscribers.Remove(subscriptionId, out _);
        }
    }

    private readonly record struct StreamKey(Guid? TenantId, Guid DashboardId);

    /// <summary>
    /// Per-stream state guarded by <see cref="Mutex"/>. <see cref="Cursor"/>
    /// is the monotonic per-<c>(tenant, dashboard)</c> stream counter;
    /// <see cref="Ring"/> is the bounded replay buffer; <see cref="Subscribers"/>
    /// is the active fan-out set.
    /// </summary>
    private sealed class StreamState(int ringCapacity)
    {
        public long Cursor;
        public Queue<WidgetPushMessage> Ring { get; } = new(ringCapacity);
        public Dictionary<Guid, ChannelWriter<WidgetPushMessage>> Subscribers { get; } = [];
        public Lock Mutex { get; } = new();
    }

    private sealed class SubscriptionHandle(
        InMemoryWidgetPushHub hub,
        StreamKey key,
        Guid subscriptionId) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            hub.Unregister(key, subscriptionId);
        }
    }
}
