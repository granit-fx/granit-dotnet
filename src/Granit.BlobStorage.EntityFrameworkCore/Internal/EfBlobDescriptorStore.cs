using Granit.BlobStorage.Domain;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.BlobStorage.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IBlobDescriptorStore"/>.
/// </summary>
/// <remarks>
/// All reads are implicitly scoped to the current tenant via the <see cref="IMultiTenant"/>
/// query filter applied by <c>ApplyGranitConventions</c> on <see cref="BlobStorageDbContext"/>.
/// Each operation creates and disposes its own <see cref="BlobStorageDbContext"/> via
/// <see cref="IDbContextFactory{TContext}"/>, making it safe for concurrent request handling.
/// </remarks>
internal sealed class EfBlobDescriptorStore(
    IDbContextFactory<BlobStorageDbContext> contextFactory)
    : EfStoreBase<BlobDescriptor, BlobStorageDbContext>(contextFactory), IBlobDescriptorStore
{
    /// <inheritdoc/>
    public Task<BlobDescriptor?> FindAsync(
        Guid blobId,
        CancellationToken cancellationToken = default) =>
        FirstOrDefaultAsync(b => b.Id == blobId, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync(
        BlobDescriptor descriptor,
        CancellationToken cancellationToken = default) =>
        AddAsync(descriptor, cancellationToken);

    /// <inheritdoc/>
    public new Task UpdateAsync(
        BlobDescriptor descriptor,
        CancellationToken cancellationToken = default) =>
        base.UpdateAsync(descriptor, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<BlobDescriptor>> FindOrphanedAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<BlobDescriptor>()
                .Where(b => (b.Status == BlobStatus.Pending || b.Status == BlobStatus.Uploading)
                            && b.CreatedAt < cutoff)
                .OrderBy(b => (object)b.CreatedAt)
                .Limit(batchSize),
            cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<BlobDescriptor>> FindByContainerBeforeAsync(
        string containerName,
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<BlobDescriptor>()
                .Where(b => b.ContainerName == containerName
                            && b.Status == BlobStatus.Valid
                            && b.CreatedAt < cutoff)
                .OrderBy(b => (object)b.CreatedAt)
                .Limit(batchSize),
            cancellationToken);
}
