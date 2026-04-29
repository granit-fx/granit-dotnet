using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.BlobStorage.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.BlobStorage.Metrics;

/// <summary>
/// Number of <see cref="BlobDescriptor"/>s in <see cref="BlobStatus.Valid"/> state —
/// the live blob population available for download. Pending / Uploading /
/// Rejected / Deleted states are excluded.
/// </summary>
public sealed class ValidBlobDescriptorCountMetricDefinition : MetricDefinition<BlobDescriptor, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.BlobStorage.ValidBlobDescriptorCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<BlobDescriptor, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<BlobDescriptor, bool>>? BaseFilter
        => b => b.Status == BlobStatus.Valid;

    /// <inheritdoc />
    public override Expression<Func<BlobDescriptor, DateTimeOffset>>? PeriodSelector
        => b => b.CreatedAt;
}
