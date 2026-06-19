namespace Granit.BlobStorage;

/// <summary>
/// Reads validated blob content by ID, under the current tenant's ACLs.
/// </summary>
/// <remarks>
/// This is the public seam for reading blob bytes server-side — for use cases where the
/// Direct-to-Cloud pre-signed URL path is not applicable (AI text extraction, GDPR export assembly,
/// virus scanning…). The default implementation is registered by <c>GranitBlobStorageModule</c>;
/// any configured storage provider is used transparently.
/// </remarks>
public interface IBlobContentReader
{
    /// <summary>
    /// Returns the content of the blob identified by <paramref name="blobId"/>,
    /// or <see langword="null"/> when the blob is not found, does not belong to the current
    /// tenant, or has a status other than <see cref="Domain.BlobStatus.Valid"/>.
    /// </summary>
    /// <param name="blobId">The blob identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<BlobContent?> ReadAsync(Guid blobId, CancellationToken cancellationToken = default);
}
