using Granit.BlobStorage.Options;

namespace Granit.BlobStorage.DbStore.Options;

/// <summary>
/// Configuration options for the database blob storage provider.
/// </summary>
/// <remarks>
/// Bound from the <c>"BlobStorage"</c> configuration section (inherits <see cref="BlobStorageOptions.SectionName"/>).
/// </remarks>
public sealed class DbStoreBlobOptions : BlobStorageOptions
{
    /// <summary>
    /// Maximum allowed blob size in bytes. Defaults to 10 MB.
    /// </summary>
    /// <remarks>
    /// Database storage is not designed for large files. Consider S3 or FileSystem providers
    /// for blobs exceeding this limit.
    /// </remarks>
    public long MaxBlobSizeBytes { get; set; } = 10 * 1024 * 1024;
}
