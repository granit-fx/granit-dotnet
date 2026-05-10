using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Documents.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Documents.Metrics;

/// <summary>
/// Number of <see cref="FolderStatus.Active"/> user-created folders. The invisible
/// tenant root is excluded so the KPI reflects the user-facing folder count.
/// </summary>
public sealed class FolderCountMetricDefinition : MetricDefinition<Folder, int>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Documents.FolderCountMetric";

    /// <inheritdoc/>
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc/>
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc/>
    public override Expression<Func<Folder, int?>>? Selector => null;

    /// <inheritdoc/>
    public override Expression<Func<Folder, bool>>? BaseFilter
        => f => f.Status == FolderStatus.Active && !f.IsTenantRoot;
}
