namespace Granit.BlobStorage.Domain;

/// <summary>
/// Lifecycle states of a <see cref="BlobDescriptor"/>.
/// </summary>
public enum BlobStatus
{
    /// <summary>Upload ticket issued; no bytes received on S3 yet.</summary>
    Pending,

    /// <summary>S3 upload notification received; validation pipeline running.</summary>
    Uploading,

    /// <summary>All validators passed; blob is accessible for download.</summary>
    Valid,

    /// <summary>
    /// Validation failed (wrong MIME type, size exceeded, antivirus hit).
    /// The S3 object has been physically deleted.
    /// </summary>
    Rejected,

    /// <summary>
    /// GDPR Art. 17 erasure: S3 object physically deleted.
    /// The <see cref="BlobDescriptor"/> record is retained for the ISO 27001 3-year audit trail.
    /// </summary>
    Deleted,
}
