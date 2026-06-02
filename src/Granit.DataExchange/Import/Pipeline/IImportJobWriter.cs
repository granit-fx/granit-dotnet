using Granit.DataExchange.Import.Domain;

namespace Granit.DataExchange.Import.Pipeline;

/// <summary>
/// Persists <see cref="ImportJob"/> entities.
/// </summary>
public interface IImportJobWriter
{
    /// <summary>
    /// Creates a new import job.
    /// </summary>
    /// <param name="job">The import job to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(ImportJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing import job.
    /// </summary>
    /// <param name="job">The import job with updated state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing import job with optimistic concurrency check.
    /// </summary>
    /// <param name="job">The import job with updated state.</param>
    /// <param name="concurrencyStamp">Client-supplied stamp from the last read; must match the stored value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(ImportJob job, string concurrencyStamp, CancellationToken cancellationToken = default);
}
