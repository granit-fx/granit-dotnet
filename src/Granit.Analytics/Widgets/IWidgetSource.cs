using System.Threading.Channels;

namespace Granit.Analytics.Widgets;

/// <summary>
/// Pull-and-push contract for any widget that produces a payload of
/// <typeparamref name="TSnapshot"/>. Locked v1 so consumer code stays the same whether
/// the host wires the framework push transport (<c>Granit.Dashboards.Push</c>,
/// ADR-043) or stays pull-only.
/// </summary>
/// <typeparam name="TSnapshot">The payload-specific snapshot shape.</typeparam>
/// <remarks>
/// <para>
/// Per EPIC #1366 future-proofing invariant #1 — both <see cref="RenderAsync"/> and
/// <see cref="SubscribeAsync"/> are exposed from v1. <see cref="RenderAsync"/> is the
/// always-on pull implementation. <see cref="SubscribeAsync"/> is the in-process
/// stream the push transport adapts onto SSE / WebSocket. Hosts without the push
/// package can still consume <see cref="SubscribeAsync"/> directly (e.g. internal
/// background processors that follow widget snapshots without round-tripping HTTP).
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
    /// Subscribes to a stream of payload updates for this widget. Producers that
    /// can't push (their data only updates on schedule) emit a single seed envelope
    /// from <see cref="RenderAsync"/> and complete the channel — the dashboard then
    /// falls back to the pull cadence implied by the effective transport policy
    /// (ADR-043 §2.3).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token; closes the channel.</param>
    /// <returns>A channel reader that emits payloads as they become available.</returns>
    /// <remarks>
    /// Implementations may emit full <see cref="WidgetPayload{TSnapshot}"/> envelopes
    /// (initial snapshot, then live updates). Delta-message support is reserved for
    /// a follow-up.
    /// </remarks>
    ChannelReader<WidgetPayload<TSnapshot>> SubscribeAsync(CancellationToken cancellationToken = default);
}
