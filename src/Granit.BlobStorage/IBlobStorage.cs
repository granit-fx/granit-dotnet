using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Options;
namespace Granit.BlobStorage;

/// <summary>
/// Primary entry-point for blob storage operations.
/// </summary>
/// <remarks>
/// All operations are scoped to the current tenant resolved via <c>ICurrentTenant</c>.
/// Direct-to-Cloud architecture: the application server never streams file bytes;
/// only metadata and Pre-signed URLs are exchanged.
/// </remarks>
public interface IBlobStorage
{
    /// <summary>
    /// Creates a <see cref="BlobDescriptor"/> in <see cref="BlobStatus.Pending"/> state
    /// and returns a Pre-signed upload ticket for direct client-to-S3 transfer.
    /// </summary>
    /// <param name="containerName">Logical container (e.g. <c>medical-images</c>).</param>
    /// <param name="request">Upload parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pre-signed upload ticket; expires after the configured TTL (default: 15 min).</returns>
    Task<PresignedUploadTicket> InitiateUploadAsync(
        string containerName,
        BlobUploadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a short-lived Pre-signed URL for direct client-to-S3 download.
    /// </summary>
    /// <param name="containerName">Logical container the blob belongs to.</param>
    /// <param name="blobId">Blob identifier returned by <see cref="InitiateUploadAsync"/>.</param>
    /// <param name="options">Optional download parameters (expiry override, filename).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pre-signed download URL; expires after the configured TTL (default: 5 min).</returns>
    /// <exception cref="Exceptions.BlobNotFoundException">Blob not found for the current tenant.</exception>
    /// <exception cref="Exceptions.BlobNotValidException">Blob exists but is not in <see cref="BlobStatus.Valid"/> state.</exception>
    Task<PresignedDownloadUrl> CreateDownloadUrlAsync(
        string containerName,
        Guid blobId,
        DownloadUrlOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <see cref="BlobDescriptor"/> for the given blob, or <c>null</c> if not found.
    /// </summary>
    /// <param name="containerName">Logical container the blob belongs to.</param>
    /// <param name="blobId">Blob identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<BlobDescriptor?> GetDescriptorAsync(
        string containerName,
        Guid blobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically deletes the S3 object and transitions the <see cref="BlobDescriptor"/>
    /// to <see cref="BlobStatus.Deleted"/> (Crypto-Shredding).
    /// </summary>
    /// <remarks>
    /// The <see cref="BlobDescriptor"/> record is <b>retained</b> in the database
    /// for the ISO 27001 3-year audit trail. Only the binary content is erased.
    /// Idempotent: calling on an already-deleted blob is a no-op.
    /// </remarks>
    /// <param name="containerName">Logical container the blob belongs to.</param>
    /// <param name="blobId">Blob identifier.</param>
    /// <param name="deletionReason">Optional reason for the audit trail (e.g. "RGPD Art. 17 erasure").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="Exceptions.BlobNotFoundException">Blob not found for the current tenant.</exception>
    Task DeleteAsync(
        string containerName,
        Guid blobId,
        string? deletionReason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a client-side upload by running the post-upload validation pipeline.
    /// </summary>
    Task<BlobConfirmationResult> ConfirmUploadAsync(
        string containerName,
        Guid blobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up orphaned blobs stuck in Pending/Uploading for over 24 hours.
    /// </summary>
    Task<int> CleanupOrphansAsync(CancellationToken cancellationToken = default);
}
