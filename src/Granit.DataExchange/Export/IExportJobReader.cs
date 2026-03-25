using Granit.DataExchange.Export.Domain;
using Granit.QueryEngine;

namespace Granit.DataExchange.Export;

/// <summary>
/// Reads export job entities.
/// </summary>
public interface IExportJobReader
{
    /// <summary>
    /// Gets an export job by ID.
    /// </summary>
    Task<ExportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists export jobs with optional status filter and offset pagination.
    /// Results are ordered by <see cref="ExportJob.CreatedAt"/> descending.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResult<ExportJob>> ListAsync(
        ExportJobStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
