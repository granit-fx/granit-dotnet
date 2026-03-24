using Granit.BlobStorage.GoogleCloud.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.GoogleCloud.Internal;

/// <summary>
/// Default <see cref="IBlobKeyStrategy"/> using tenant-prefix isolation for Google Cloud Storage.
/// </summary>
/// <remarks>
/// The date components improve GCS performance on large buckets by distributing
/// keys across a wider key-space prefix, reducing hot-spot partitions.
/// </remarks>
internal sealed class GoogleCloudBlobKeyStrategy(
    ICurrentTenant currentTenant,
    IClock clock,
    IOptions<GoogleCloudStorageOptions> options) : TenantPrefixBlobKeyStrategy(currentTenant, clock)
{
    /// <inheritdoc/>
    public override string ResolveBucketName(string containerName) => options.Value.DefaultBucket;
}
