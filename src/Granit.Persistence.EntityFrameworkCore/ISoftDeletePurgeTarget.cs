namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Marker interface for services that can purge soft-deleted records past the ISO 27001 retention period.
/// </summary>
/// <remarks>
/// Each module that owns a <see cref="Microsoft.EntityFrameworkCore.DbContext"/> containing
/// <see cref="Granit.Domain.ISoftDeletable"/> entities should register an implementation
/// of this interface. The <c>SoftDeletePurge</c> recurring job collects all implementations
/// and purges each target.
/// </remarks>
public interface ISoftDeletePurgeTarget
{
    /// <summary>
    /// Hard-deletes soft-deleted records older than <paramref name="cutoff"/> from this target's store.
    /// </summary>
    /// <param name="cutoff">Records with <c>DeletedAt</c> strictly before this value are purged.</param>
    /// <param name="batchSize">Maximum number of records to delete per entity type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total number of records deleted.</returns>
    Task<int> PurgeAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken = default);
}
