using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.BlobStorage.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.BlobStorage.Metrics;

/// <summary>
/// Number of <see cref="BlobDescriptor"/>s stuck in pre-validation states —
/// <see cref="BlobStatus.Pending"/> (upload ticket issued but no bytes received)
/// or <see cref="BlobStatus.Uploading"/> (S3 notification received but the
/// validation pipeline never finalised). High values typically indicate a
/// failed upload flow (broken pre-signed URL, missing notification webhook,
/// stalled validator). Lower is better.
/// </summary>
public sealed class OrphanBlobDescriptorCountMetricDefinition : MetricDefinition<BlobDescriptor, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.BlobStorage.OrphanBlobDescriptorCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<BlobDescriptor, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<BlobDescriptor, bool>>? BaseFilter
        => b => b.Status == BlobStatus.Pending || b.Status == BlobStatus.Uploading;

    /// <inheritdoc />
    public override Expression<Func<BlobDescriptor, DateTimeOffset>>? PeriodSelector
        => b => b.CreatedAt;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
