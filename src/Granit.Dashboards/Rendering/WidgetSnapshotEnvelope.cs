using System.Text.Json;
using Granit.Analytics.Metrics;

namespace Granit.Dashboards.Rendering;

/// <summary>
/// Non-generic envelope returned by every <see cref="IWidgetInstanceRenderer"/>.
/// Bundles the runtime <see cref="Status"/>, the declarative <see cref="WidgetType"/>,
/// and a pre-serialised <see cref="Snapshot"/> so the dashboard render endpoint
/// can compose heterogeneous widgets into one response without leaking per-kind
/// generics — see ADR-039 §2.
/// </summary>
/// <param name="Status">Runtime outcome (snapshot / unavailable / error).</param>
/// <param name="WidgetType">Declarative kind discriminator — mirrors <c>WidgetDefinition</c>'s <c>[JsonDerivedType]</c> tag (<c>"Kpi"</c>, <c>"Chart"</c>, <c>"Markdown"</c>, …). Carried even on <see cref="WidgetSnapshotStatus.Unavailable"/> / <see cref="WidgetSnapshotStatus.Error"/> so the frontend keeps a typed slot for the absent widget.</param>
/// <param name="Snapshot">
/// Pre-serialised typed payload. <see langword="null"/> when <see cref="Status"/>
/// is not <see cref="WidgetSnapshotStatus.Snapshot"/>. The renderer is responsible
/// for materialising its typed snapshot via
/// <c>JsonSerializer.SerializeToElement(payload.Snapshot, options)</c> — see ADR-039
/// §2.1 for why we converge on <see cref="JsonElement"/> at the boundary instead of
/// <c>object?</c> (System.Text.Json serialises <c>object</c> by declared type, not
/// runtime type).
/// </param>
/// <param name="Sequence">Always <c>1</c> in pull mode; future push transport increments per (widget instance, tenant). Locked v1 per EPIC #1366 invariant #2.</param>
/// <param name="EmittedAt">Server-side timestamp of the computation.</param>
/// <param name="RefreshHint">Pull / push transport hint inherited from the underlying definition.</param>
/// <param name="UnavailableReasonLocalizationKey">Localization key for the user-facing reason (e.g. <c>"Widget:Unavailable"</c>) when <see cref="Status"/> is <see cref="WidgetSnapshotStatus.Unavailable"/>; <see langword="null"/> otherwise.</param>
public sealed record WidgetSnapshotEnvelope(
    WidgetSnapshotStatus Status,
    string WidgetType,
    JsonElement? Snapshot,
    long Sequence,
    DateTimeOffset EmittedAt,
    RefreshHint RefreshHint,
    string? UnavailableReasonLocalizationKey = null)
{
    /// <summary>Snapshot envelope — the typed payload is in <paramref name="snapshot"/>, status is <see cref="WidgetSnapshotStatus.Snapshot"/>.</summary>
    public static WidgetSnapshotEnvelope ForSnapshot(
        string widgetType,
        JsonElement snapshot,
        long sequence,
        DateTimeOffset emittedAt,
        RefreshHint refreshHint)
        => new(WidgetSnapshotStatus.Snapshot, widgetType, snapshot, sequence, emittedAt, refreshHint);

    /// <summary>Unavailable envelope — the user lacks the widget's required permission, or the underlying metric / query is otherwise unreadable.</summary>
    public static WidgetSnapshotEnvelope Unavailable(
        string widgetType,
        long sequence,
        DateTimeOffset emittedAt,
        RefreshHint refreshHint,
        string reasonLocalizationKey = "Widget:Unavailable")
        => new(WidgetSnapshotStatus.Unavailable, widgetType, Snapshot: null, sequence, emittedAt, refreshHint, reasonLocalizationKey);

    /// <summary>Error envelope — the renderer threw. The dashboard bundle stays 200; the error is logged server-side, never surfaced verbatim to the client.</summary>
    public static WidgetSnapshotEnvelope Error(
        string widgetType,
        long sequence,
        DateTimeOffset emittedAt,
        RefreshHint refreshHint)
        => new(WidgetSnapshotStatus.Error, widgetType, Snapshot: null, sequence, emittedAt, refreshHint);
}
