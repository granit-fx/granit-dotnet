using Granit.Analytics.Metrics;
using Granit.Dashboards;

namespace Granit.Analytics.Rendering;

/// <summary>
/// Result of an <see cref="IDatasourceEvaluator{TDatasource}"/> evaluation for a
/// KPI widget — either a successful <see cref="MetricSnapshotPayload"/> or an
/// "unavailable" sentinel carrying a localization key. The
/// <see cref="KpiWidgetInstanceRenderer"/> reuses this shape regardless of which
/// concrete <c>Datasource</c> kind produced it, so the dispatch site stays a
/// pure switch with no per-kind envelope plumbing.
/// </summary>
/// <remarks>
/// <para>
/// Returning an envelope-shaped result (rather than raising an exception) lets
/// stubbed datasources (<see cref="QueryAggregateDatasourceEvaluator"/>,
/// <see cref="TelemetryDatasourceEvaluator"/>) participate in the dispatch
/// without leaking the cross-cutting "unavailable" concept into the renderer.
/// The renderer maps it 1-to-1 onto <c>WidgetSnapshotEnvelope.ForSnapshot</c> /
/// <c>Unavailable</c> per ADR-039 §7.bis.
/// </para>
/// </remarks>
/// <param name="Payload">The KPI snapshot payload — non-null when the evaluation succeeded; <see langword="null"/> when the datasource yielded no usable result.</param>
/// <param name="RefreshHint">The refresh hint surfaced on the wire envelope. For metric paths it mirrors the underlying <c>MetricDefinition.RefreshHint</c>; for stub paths it is <see cref="RefreshHint.Static"/>.</param>
/// <param name="ReasonLocalizationKey">Localization key surfaced on the unavailable envelope. Ignored when <paramref name="Payload"/> is non-null. Defaults to <c>Widget:Unavailable</c> when null on an unavailable result.</param>
public sealed record KpiEvaluation(
    MetricSnapshotPayload? Payload,
    RefreshHint RefreshHint,
    string? ReasonLocalizationKey = null)
{
    /// <summary>Builds a successful evaluation carrying the supplied payload.</summary>
    public static KpiEvaluation Snapshot(MetricSnapshotPayload payload, RefreshHint refreshHint)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new KpiEvaluation(payload, refreshHint);
    }

    /// <summary>Builds an "unavailable" evaluation carrying a localization key.</summary>
    public static KpiEvaluation Unavailable(RefreshHint refreshHint, string reasonLocalizationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonLocalizationKey);
        return new KpiEvaluation(Payload: null, refreshHint, reasonLocalizationKey);
    }
}
