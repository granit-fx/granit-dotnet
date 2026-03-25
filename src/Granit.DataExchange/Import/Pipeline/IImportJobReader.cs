using Granit.DataExchange.Import.Domain;
using Granit.QueryEngine;

namespace Granit.DataExchange.Import.Pipeline;

/// <summary>
/// Reads <see cref="ImportJob"/> entities.
/// </summary>
public interface IImportJobReader
{
    /// <summary>
    /// Loads an import job by identifier.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import job, or <c>null</c> if not found.</returns>
    Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists import jobs with optional status filter and offset pagination.
    /// Results are ordered by <see cref="ImportJob.CreatedAt"/> descending.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResult<ImportJob>> ListAsync(
        ImportJobStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
