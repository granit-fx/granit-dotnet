using Granit.BlobStorage.S3.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.S3.Internal;

/// <summary>
/// Default <see cref="IBlobKeyStrategy"/> using tenant-prefix isolation.
/// </summary>
/// <remarks>
/// The date components improve S3 performance on large buckets by distributing
/// keys across a wider key-space prefix, reducing hot-spot partitions.
/// </remarks>
internal sealed class PrefixBlobKeyStrategy(
    ICurrentTenant currentTenant,
    IClock clock,
    IOptions<S3BlobOptions> options) : TenantPrefixBlobKeyStrategy(currentTenant, clock)
{
    /// <inheritdoc/>
    public override string ResolveBucketName(string containerName) => options.Value.DefaultBucket;
}
