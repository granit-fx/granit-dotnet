using Granit.ReferenceData.Domain;

namespace Granit.ReferenceData;

/// <summary>
/// Write-side contract for managing reference data entries.
/// </summary>
/// <typeparam name="TEntity">The concrete reference data entity type.</typeparam>
public interface IReferenceDataStoreWriter<in TEntity> where TEntity : ReferenceDataEntity
{
    /// <summary>
    /// Creates a new reference data entry. Idempotent: if an entry with the same
    /// <see cref="ReferenceDataEntity.Code"/> already exists (even when inactive),
    /// the call is a no-op and no exception is thrown.
    /// </summary>
    /// <param name="entity">The entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing reference data entry identified by its <see cref="ReferenceDataEntity.Code"/>.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates or deactivates a reference data entry (soft toggle).
    /// </summary>
    /// <param name="code">The business key of the entry.</param>
    /// <param name="isActive">The new active status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetActiveAsync(string code, bool isActive, CancellationToken cancellationToken = default);
}
