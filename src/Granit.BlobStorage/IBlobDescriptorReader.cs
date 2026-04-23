using Granit.BlobStorage.Domain;

namespace Granit.BlobStorage;

/// <summary>
/// Read-only persistence abstraction for <see cref="BlobDescriptor"/> records.
/// </summary>
/// <remarks>
/// Implemented by <c>Granit.BlobStorage.EntityFrameworkCore</c>.
/// All reads are implicitly scoped to the current tenant.
/// </remarks>
public interface IBlobDescriptorReader
{
    /// <summary>
    /// Returns the descriptor for <paramref name="blobId"/> within the current tenant,
    /// or <c>null</c> if not found.
    /// </summary>
    Task<BlobDescriptor?> FindAsync(Guid blobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns descriptors stuck in <see cref="BlobStatus.Pending"/> or <see cref="BlobStatus.Uploading"/>
    /// state since before <paramref name="cutoff"/>, limited to <paramref name="batchSize"/> results.
    /// </summary>
    /// <remarks>
    /// Used by orphan-cleanup background jobs to detect uploads that never completed
    /// (client abandoned, network failure, S3 timeout). The caller is responsible for
    /// transitioning the returned descriptors to <see cref="BlobStatus.Rejected"/>.
    /// </remarks>
    /// <param name="cutoff">Only descriptors created before this instant are returned.</param>
    /// <param name="batchSize">Maximum number of descriptors to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<BlobDescriptor>> FindOrphanedAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see cref="BlobStatus.Valid"/> descriptors in the given <paramref name="containerName"/>
    /// created before <paramref name="cutoff"/>, limited to <paramref name="batchSize"/> results.
    /// </summary>
    /// <remarks>
    /// Used by GDPR cleanup background jobs to purge temporary containers (e.g. GDPR export archives)
    /// after the retention period expires.
    /// </remarks>
    /// <param name="containerName">The blob container to search.</param>
    /// <param name="cutoff">Only descriptors created before this instant are returned.</param>
    /// <param name="batchSize">Maximum number of descriptors to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<BlobDescriptor>> FindByContainerBeforeAsync(
        string containerName,
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);
}
