namespace Granit.Dashboards.Rendering;

/// <summary>
/// Runtime outcome of a single widget render — the "what happened" axis of
/// the per-widget envelope. Decoupled from <see cref="WidgetSnapshotEnvelope.WidgetType"/>
/// (the declarative kind) so the frontend's `useDashboard` hook can branch on
/// "render the snapshot" vs "render an unavailable / error placeholder"
/// without inspecting the snapshot payload itself.
/// </summary>
/// <remarks>
/// See ADR-039 §2 — the original draft conflated runtime status and declarative
/// kind onto a single <c>kind</c> field. The split keeps the wire schema
/// unambiguous on degraded responses (<c>status: "Unavailable"</c> /
/// <c>"Error"</c>).
/// </remarks>
public enum WidgetSnapshotStatus
{
    /// <summary>The renderer produced a typed snapshot — the <c>Snapshot</c> field is non-null.</summary>
    Snapshot,

    /// <summary>The current user lacks the widget's required permission. The dashboard render endpoint short-circuits before invoking the underlying metric / query.</summary>
    Unavailable,

    /// <summary>The renderer threw an unexpected exception. Logged server-side; the bundle response stays 200 so a single bad widget does not break the whole dashboard.</summary>
    Error,
}
