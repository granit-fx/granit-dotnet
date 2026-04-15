using System.ComponentModel.DataAnnotations;

namespace Granit.DataExchange.BlobStorage.Options;

/// <summary>
/// Configuration for the blob-storage-backed <see cref="IDataExchangeFileProvider"/>.
/// </summary>
public sealed class DataExchangeBlobStorageOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Granit:DataExchange:BlobStorage";

    /// <summary>
    /// Logical container name used as a key-prefix segment in the object key path
    /// (e.g. <c>{tenantId}/data-exchange/2026/04/{blobId}</c>).
    /// This is NOT a physical bucket — the physical bucket is resolved by
    /// <c>IBlobKeyStrategy.ResolveBucketName</c> (typically <c>S3BlobOptions.DefaultBucket</c>).
    /// </summary>
    [Required]
    public string ContainerName { get; set; } = "data-exchange";

    /// <summary>
    /// Default MIME type applied when storing export files.
    /// Import uploads preserve the original content type via the file name extension.
    /// </summary>
    public string DefaultContentType { get; set; } = "application/octet-stream";
}
