using Granit.Core.MultiTenancy;
using Granit.Timing;

namespace Granit.BlobStorage.DbStore.Internal;

/// <summary>
/// Builds tenant-prefixed object keys for database blob storage.
/// </summary>
/// <remarks>
/// The bucket name is unused — all blobs share the same database table.
/// </remarks>
internal sealed class DbStoreBlobKeyStrategy(
    ICurrentTenant currentTenant,
    IClock clock) : TenantPrefixBlobKeyStrategy(currentTenant, clock)
{
    /// <inheritdoc/>
    public override string ResolveBucketName(string containerName) => "dbstore";
}
