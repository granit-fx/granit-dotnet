using System.Threading.Channels;

namespace Granit.Dashboards.Push.Internal;

/// <summary>
/// Hub-internal contract that bridges the public <see cref="IWidgetPushPublisher"/>
/// (used by producer modules) and the SSE endpoint (which subscribes to receive
/// envelopes for a given <c>(tenantId, dashboardId)</c> stream).
/// </summary>
/// <remarks>
/// Internal so external consumers never reach for the <see cref="Subscribe"/>
/// path directly — they subscribe by opening the SSE endpoint. The hub
/// implementation is what the DI extension binds to both
/// <see cref="IWidgetPushPublisher"/> and this interface.
/// </remarks>
internal interface IWidgetPushHub : IWidgetPushPublisher
{
    /// <summary>
    /// Registers <paramref name="writer"/> as a subscriber for the given
    /// <c>(tenantId, dashboardId)</c> stream. Atomic with the ring snapshot so
    /// no envelope is lost between the replay and the live drain. ADR-043 §5.
    /// </summary>
    /// <param name="tenantId">Tenant scope (matches the publisher's tenantId on the same stream).</param>
    /// <param name="dashboardId">Dashboard the subscriber is following.</param>
    /// <param name="lastEventId">
    /// SSE <c>Last-Event-ID</c> sent by reconnecting clients. <see langword="null"/> for
    /// fresh subscribers — they get no replay. When non-null, the hub returns the
    /// buffered messages with <c>StreamCursor &gt; lastEventId</c>; if the client is
    /// behind the oldest ring entry, <see cref="SubscriptionResult.ResumeFailed"/>
    /// is set and replay is empty.
    /// </param>
    /// <param name="writer">Channel writer that receives live envelopes after the replay.</param>
    SubscriptionResult Subscribe(
        Guid? tenantId,
        Guid dashboardId,
        long? lastEventId,
        ChannelWriter<WidgetPushMessage> writer);
}
