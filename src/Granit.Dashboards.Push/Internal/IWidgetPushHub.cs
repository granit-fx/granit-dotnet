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
    /// <c>(tenantId, dashboardId)</c> stream. Returns an
    /// <see cref="IDisposable"/> that unregisters and (optionally) completes
    /// the writer on disposal — the caller (SSE endpoint) disposes when the
    /// HTTP connection closes or cancellation fires.
    /// </summary>
    IDisposable Subscribe(
        Guid? tenantId,
        Guid dashboardId,
        ChannelWriter<WidgetPushMessage> writer);
}
