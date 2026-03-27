namespace Granit.BlobStorage.Options;

/// <summary>
/// Core options shared by all blob storage providers.
/// Provider-specific options (e.g. <c>S3BlobOptions</c> in <c>Granit.BlobStorage.S3</c>) extend this class.
/// </summary>
public class BlobStorageOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "BlobStorage";

    /// <summary>TTL for Pre-signed upload URLs. Defaults to 15 minutes.</summary>
    public TimeSpan UploadUrlExpiry { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>TTL for Pre-signed download URLs. Defaults to 5 minutes.</summary>
    public TimeSpan DownloadUrlExpiry { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// When non-empty, only blobs with a declared content type in this set are accepted.
    /// Checked before magic-byte validation (Order 5). Case-insensitive comparison.
    /// </summary>
    public HashSet<string> AllowedContentTypes { get; set; } = [];

    /// <summary>
    /// When <c>true</c>, the magic-byte validator rejects files whose binary signature
    /// cannot be verified. Defaults to <c>false</c> (pass-through).
    /// </summary>
    public bool RejectUnverifiedContentTypes { get; set; }
}
