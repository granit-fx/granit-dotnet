namespace Granit.Analytics.Metrics;

/// <summary>
/// Indicates how often a metric's underlying data is expected to change. Drives the
/// caching layer's TTL and signals to the frontend which transport to use (pull vs push).
/// </summary>
/// <remarks>
/// <para>
/// Pure pull-based widgets (the v1 reality) honor <see cref="Static"/> and <see cref="Dynamic"/>
/// to pick a TTL. <see cref="Realtime"/> is reserved for future push-based transport delivered
/// by the <c>granit-iot</c> repository (WebSocket / SSE) — pull-based renderers should reject
/// metrics declaring it (architecture-test enforced) until that transport ships.
/// </para>
/// </remarks>
public enum RefreshHint
{
    /// <summary>Long-lived data (5+ min cache acceptable). E.g. tenant settings, role catalog.</summary>
    Static,

    /// <summary>Short-lived but pull-friendly (60–120 s cache). v1 default for most KPIs.</summary>
    Dynamic,

    /// <summary>Push-only, sub-second updates. Reserved — requires <c>granit-iot</c> transport.</summary>
    Realtime,
}
