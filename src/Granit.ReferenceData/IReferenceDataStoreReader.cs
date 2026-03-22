using Granit.Querying;
using Granit.ReferenceData.Domain;

namespace Granit.ReferenceData;

/// <summary>
/// Read-side contract for querying reference data entries.
/// </summary>
/// <typeparam name="TEntity">The concrete reference data entity type.</typeparam>
public interface IReferenceDataStoreReader<TEntity> where TEntity : ReferenceDataEntity
{
    /// <summary>
    /// Retrieves a filtered, sorted, and paginated list of reference data entries.
    /// </summary>
    /// <param name="query">Optional query parameters. When <c>null</c>, returns all active entries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PagedResult{T}"/> containing the matching items and total count.</returns>
    Task<PagedResult<TEntity>> GetAllAsync(
        ReferenceDataQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single entry by its unique <see cref="ReferenceDataEntity.Code"/>.
    /// </summary>
    /// <param name="code">The business key (e.g., "BE").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching entity, or <c>null</c> if not found.</returns>
    Task<TEntity?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all direct children of a parent entry in a hierarchical reference data type.
    /// </summary>
    /// <param name="parentCode">
    /// The parent's <see cref="ReferenceDataEntity.Code"/>. Pass <c>null</c> to get root entries.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of child entries, ordered by <see cref="ReferenceDataEntity.SortOrder"/>.</returns>
    Task<IReadOnlyList<TEntity>> GetChildrenAsync(
        string? parentCode,
        CancellationToken cancellationToken = default);
}
