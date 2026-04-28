using System.Threading.Channels;

namespace Granit.Analytics.Widgets;

/// <summary>
/// Pull-and-future-push contract for any widget that produces a payload of
/// <typeparamref name="TSnapshot"/>. Locked v1 so push transport can be added later
/// (in <c>granit-iot</c>) without rewriting consumer code.
/// </summary>
/// <typeparam name="TSnapshot">The payload-specific snapshot shape.</typeparam>
/// <remarks>
/// <para>
/// Per EPIC #1366 future-proofing invariant #1 — both <see cref="RenderAsync"/> and
/// <see cref="SubscribeAsync"/> are exposed from v1. <see cref="RenderAsync"/> is the
/// real implementation; <see cref="SubscribeAsync"/> ships a poll-emulation default
/// that re-runs <see cref="RenderAsync"/> on the cadence implied by the metric's
/// <see cref="Metrics.RefreshHint"/>. Real push (WebSocket / SSE) replaces the
/// emulation when the <c>granit-iot</c> transport lands.
/// </para>
/// </remarks>
public interface IWidgetSource<TSnapshot>
{
    /// <summary>
    /// Computes the current snapshot for this widget — pull mode.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The widget payload envelope (snapshot + sequence + emitted-at + refresh hint).</returns>
    Task<WidgetPayload<TSnapshot>> RenderAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to a stream of payload updates for this widget. v1 default emulates push
    /// by repeatedly invoking <see cref="RenderAsync"/> — real push transport (WebSocket / SSE)
    /// will replace this in the <c>granit-iot</c> repo.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token; closes the channel.</param>
    /// <returns>A channel reader that emits payloads as they become available.</returns>
    /// <remarks>
    /// Implementations may emit either full <see cref="WidgetPayload{TSnapshot}"/> envelopes
    /// (initial snapshot) or future delta messages. v1 emits snapshots only.
    /// </remarks>
    ChannelReader<WidgetPayload<TSnapshot>> SubscribeAsync(CancellationToken cancellationToken = default);
}
