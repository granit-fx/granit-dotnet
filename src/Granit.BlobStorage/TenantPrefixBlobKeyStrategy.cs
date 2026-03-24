using Granit.MultiTenancy;
using Granit.Timing;

namespace Granit.BlobStorage;

/// <summary>
/// Base <see cref="IBlobKeyStrategy"/> using tenant-prefix isolation.
/// </summary>
/// <remarks>
/// Object key format (multi-tenant): <c>{tenantId}/{containerName}/{yyyy}/{MM}/{blobId}</c>
/// Object key format (single-tenant): <c>{containerName}/{yyyy}/{MM}/{blobId}</c>
/// <para>
/// The date components distribute blobs across a wider key-space prefix,
/// reducing hot-spot partitions and improving list-operation performance.
/// </para>
/// <para>
/// Subclasses must override <see cref="ResolveBucketName"/> to return the
/// provider-specific physical location (S3 bucket, Azure container, file path, etc.).
/// </para>
/// </remarks>
public abstract class TenantPrefixBlobKeyStrategy(
    ICurrentTenant currentTenant,
    IClock clock) : IBlobKeyStrategy
{
    /// <inheritdoc/>
    public string BuildObjectKey(string containerName, Guid blobId)
    {
        string? tenantId = currentTenant.IsAvailable && currentTenant.Id is not null
            ? currentTenant.Id.Value.ToString()
            : null;
        DateTimeOffset now = clock.Now;
        return tenantId is not null
            ? $"{tenantId}/{containerName}/{now:yyyy}/{now:MM}/{blobId}"
            : $"{containerName}/{now:yyyy}/{now:MM}/{blobId}";
    }

    /// <inheritdoc/>
    public abstract string ResolveBucketName(string containerName);

    /// <inheritdoc/>
    public bool TryExtractTenantId(string objectKey, out string? tenantId)
    {
        if (string.IsNullOrEmpty(objectKey))
        {
            tenantId = null;
            return false;
        }

        int slashIndex = objectKey.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex <= 0)
        {
            tenantId = null;
            return false;
        }

        tenantId = objectKey[..slashIndex];
        return !string.IsNullOrEmpty(tenantId);
    }
}
