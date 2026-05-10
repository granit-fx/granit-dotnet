using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Documents.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Documents.Metrics;

/// <summary>
/// Number of <see cref="DocumentStatus.Active"/> documents in the tenant. Trashed and
/// permanently-deleted rows are excluded by design — the KPI tracks the live document
/// surface, not the audit tombstone count.
/// </summary>
/// <remarks>
/// <para>
/// Period selector is unset: the count is a live snapshot, not a per-period rate. Pair
/// with <c>?period=…</c> on a creation-driven metric (e.g. a future
/// <c>DocumentsCreatedCountMetric</c>) when a periodised view is needed.
/// </para>
/// </remarks>
public sealed class DocumentCountMetricDefinition : MetricDefinition<Document, int>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Documents.DocumentCountMetric";

    /// <inheritdoc/>
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc/>
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc/>
    public override Expression<Func<Document, int?>>? Selector => null;

    /// <inheritdoc/>
    public override Expression<Func<Document, bool>>? BaseFilter
        => d => d.Status == DocumentStatus.Active;
}
