using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Granit.Analytics.Metrics;
using Granit.Dashboards.Rendering;

namespace Granit.Dashboards.Push.Internal;

/// <summary>
/// In-process implementation of <see cref="IWidgetPushHub"/>. Producers call
/// <see cref="PublishSnapshotAsync"/> / <see cref="PublishUnavailableAsync"/>;
/// the SSE endpoint subscribes via <see cref="Subscribe"/>; the hub fans out
/// inside the same process.
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
/// loses the in-flight message; the SSE endpoint shapes its channel as
/// unbounded with <see cref="BoundedChannelFullMode.DropOldest"/> on the
/// rendering side, so practical loss only happens when the client has
/// disconnected and is about to be unregistered.
/// </para>
/// </remarks>
internal sealed class InMemoryWidgetPushHub(WidgetPushSequenceAllocator sequenceAllocator) : IWidgetPushHub
{
    private readonly WidgetPushSequenceAllocator _sequenceAllocator = sequenceAllocator;

    // ConcurrentDictionary<StreamKey, ConcurrentDictionary<SubscriptionId, ChannelWriter>>.
    // Outer key partitions per (tenant, dashboard); inner per active subscription.
    private readonly ConcurrentDictionary<StreamKey, ConcurrentDictionary<Guid, ChannelWriter<WidgetPushMessage>>> _streams = new();

    /// <inheritdoc/>
    public Task PublishSnapshotAsync(
        Guid? tenantId,
        Guid dashboardId,
        Guid widgetInstanceId,
        string widgetType,
        JsonElement snapshot,
        DateTimeOffset emittedAt,
        RefreshHint refreshHint,
        CancellationToken cancellationToken = default)
    {
        long sequence = _sequenceAllocator.Next(tenantId, widgetInstanceId);
        var envelope = WidgetSnapshotEnvelope.ForSnapshot(
            widgetType, snapshot, sequence, emittedAt, refreshHint);

        Dispatch(tenantId, dashboardId, widgetInstanceId, envelope);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task PublishUnavailableAsync(
        Guid? tenantId,
        Guid dashboardId,
        Guid widgetInstanceId,
        string widgetType,
        DateTimeOffset emittedAt,
        RefreshHint refreshHint,
        string reasonLocalizationKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonLocalizationKey);

        long sequence = _sequenceAllocator.Next(tenantId, widgetInstanceId);
        var envelope = WidgetSnapshotEnvelope.Unavailable(
            widgetType, sequence, emittedAt, refreshHint, reasonLocalizationKey);

        Dispatch(tenantId, dashboardId, widgetInstanceId, envelope);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(
        Guid? tenantId,
        Guid dashboardId,
        ChannelWriter<WidgetPushMessage> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var key = new StreamKey(tenantId, dashboardId);
        ConcurrentDictionary<Guid, ChannelWriter<WidgetPushMessage>> subs = _streams.GetOrAdd(
            key, _ => new ConcurrentDictionary<Guid, ChannelWriter<WidgetPushMessage>>());

#pragma warning disable GRSEC002 // In-process subscription id, opaque, never persisted — sequential UUID would buy nothing.
        var subscriptionId = Guid.NewGuid();
#pragma warning restore GRSEC002
        subs[subscriptionId] = writer;

        return new SubscriptionHandle(this, key, subscriptionId);
    }

    private void Dispatch(
        Guid? tenantId,
        Guid dashboardId,
        Guid widgetInstanceId,
        WidgetSnapshotEnvelope envelope)
    {
        StreamKey key = new(tenantId, dashboardId);
        if (!_streams.TryGetValue(key, out ConcurrentDictionary<Guid, ChannelWriter<WidgetPushMessage>>? subs))
        {
            return;
        }

        WidgetPushMessage message = new(tenantId, dashboardId, widgetInstanceId, envelope);
        foreach (ChannelWriter<WidgetPushMessage> writer in subs.Values)
        {
            // TryWrite is non-blocking. Slow / closed subscribers silently drop —
            // the SSE handler unregisters on disconnect via the SubscriptionHandle.
            writer.TryWrite(message);
        }
    }

    private void Unregister(StreamKey key, Guid subscriptionId)
    {
        if (_streams.TryGetValue(key, out ConcurrentDictionary<Guid, ChannelWriter<WidgetPushMessage>>? subs))
        {
            subs.TryRemove(subscriptionId, out _);
        }
    }

    private readonly record struct StreamKey(Guid? TenantId, Guid DashboardId);

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
