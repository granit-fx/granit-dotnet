namespace Granit.Dashboards.Push.Internal;

/// <summary>
/// Outcome of <see cref="IWidgetPushHub.Subscribe"/>: the (possibly empty)
/// replay snapshot, the current server cursor, and the disposable handle the
/// caller releases on disconnect. ADR-043 §5.
/// </summary>
/// <param name="ResumeFailed">
/// <see langword="true"/> when the caller passed a <c>Last-Event-ID</c> that is
/// older than the oldest entry still in the ring buffer — the client missed
/// envelopes that have since aged out and must re-pull the seed via
/// <c>POST /dashboards/{id}/render</c>. The SSE handler emits a dedicated
/// <c>event: resume-failed</c> frame so the frontend hook knows to fall back.
/// </param>
/// <param name="ServerCursor">
/// Current server-side stream cursor at subscribe time. Informational —
/// surfaced so test harnesses can pin behaviour without reflecting on the
/// hub's internal state.
/// </param>
/// <param name="Replay">
/// Buffered messages with <c>StreamCursor &gt; lastEventId</c>, in cursor order.
/// Empty when no resume was requested or when the client is fully caught up.
/// </param>
/// <param name="Handle">
/// Subscription handle. Disposing unregisters the writer from the stream's
/// fan-out list. Once disposed, the hub will not call <c>TryWrite</c> on the
/// associated channel again.
/// </param>
internal sealed record SubscriptionResult(
    bool ResumeFailed,
    long ServerCursor,
    IReadOnlyList<WidgetPushMessage> Replay,
    IDisposable Handle);
