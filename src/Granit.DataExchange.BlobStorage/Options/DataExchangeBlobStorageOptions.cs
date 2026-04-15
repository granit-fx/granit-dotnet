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
    /// Logical container name used for data exchange files (import uploads and export outputs).
    /// </summary>
    [Required]
    public string ContainerName { get; set; } = "data-exchange";

    /// <summary>
    /// Default MIME type applied when storing export files.
    /// Import uploads preserve the original content type via the file name extension.
    /// </summary>
    public string DefaultContentType { get; set; } = "application/octet-stream";
}
