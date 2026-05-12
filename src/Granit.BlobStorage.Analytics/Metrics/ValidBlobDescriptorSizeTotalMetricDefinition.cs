using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.BlobStorage.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.BlobStorage.Analytics.Metrics;

/// <summary>
/// Sum of <see cref="BlobDescriptor.SizeBytes"/> across blobs in
/// <see cref="BlobStatus.Valid"/> state — total live storage footprint in
/// bytes. The host renders the value with a unit suffix in the UI; the
/// metric stays unit-agnostic at the framework level.
/// </summary>
/// <remarks>
/// <see cref="BlobDescriptor.SizeBytes"/> is nullable on the entity (only set
/// once the validation pipeline has confirmed the upload). Restricting the
/// base filter to <c>Status == Valid</c> guarantees the projection always
/// resolves a real number — but the selector's <c>long?</c> typing is kept
/// for the empty-set safety EF Core needs.
/// </remarks>
public sealed class ValidBlobDescriptorSizeTotalMetricDefinition : MetricDefinition<BlobDescriptor, long>
{
    /// <inheritdoc />
    public override string Name => "Granit.BlobStorage.ValidBlobDescriptorSizeTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Number;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<BlobDescriptor, long?>>? Selector
        => b => b.SizeBytes;

    /// <inheritdoc />
    public override Expression<Func<BlobDescriptor, bool>>? BaseFilter
        => b => b.Status == BlobStatus.Valid;

    /// <inheritdoc />
    public override Expression<Func<BlobDescriptor, DateTimeOffset>>? PeriodSelector
        => b => b.CreatedAt;
}
