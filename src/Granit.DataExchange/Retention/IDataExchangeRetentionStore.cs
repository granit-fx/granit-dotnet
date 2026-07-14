using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Import.Domain;

namespace Granit.DataExchange.Retention;

/// <summary>
/// Persistence surface for the GDPR retention sweep (<c>Granit.DataExchange.BackgroundJobs</c>):
/// finds jobs whose stored files or database rows have outlived their retention window, plus
/// jobs stranded in a non-terminal executing state, and applies the resulting updates/deletes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cross-tenant contract.</b> The sweep runs as a system job with <b>no ambient tenant</b>.
/// Implementations MUST query across all tenant partitions (host partition included) through the
/// sanctioned cross-tenant mechanism (<c>EfStoreBase.QueryAcrossTenants</c>, which records the
/// <c>granit.persistence.cross_tenant_query</c> metric). Relying on the implicit multi-tenant
/// filter fails CLOSED to the host partition and silently skips every real tenant's expired
/// data — the exact GDPR storage-limitation gap this store exists to close.
/// </para>
/// <para>
/// A fail-fast null-object is registered by default; installing
/// <c>Granit.DataExchange.EntityFrameworkCore</c> (via <c>AddGranitDataExchangeEntityFrameworkCore</c>)
/// replaces it with the EF Core implementation.
/// </para>
/// </remarks>
public interface IDataExchangeRetentionStore
{
    /// <summary>
    /// Returns terminal import jobs completed before <paramref name="completedBefore"/> whose
    /// uploaded file has not been deleted yet (<see cref="ImportJob.FileDeletedAt"/> is
    /// <see langword="null"/>), oldest first, capped at <paramref name="batchSize"/>.
    /// </summary>
    Task<IReadOnlyList<ImportJob>> GetImportJobsWithExpiredFilesAsync(
        DateTimeOffset completedBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns completed export jobs finished before <paramref name="completedBefore"/> whose
    /// generated file has not been deleted yet (<see cref="ExportJob.FileDeletedAt"/> is
    /// <see langword="null"/>), oldest first, capped at <paramref name="batchSize"/>.
    /// </summary>
    Task<IReadOnlyList<ExportJob>> GetExportJobsWithExpiredFilesAsync(
        DateTimeOffset completedBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns terminal import jobs whose last lifecycle activity (completion, or last
    /// modification for cancelled jobs that never set <see cref="ImportJob.CompletedAt"/>)
    /// predates <paramref name="terminalBefore"/>, oldest first, capped at <paramref name="batchSize"/>.
    /// </summary>
    Task<IReadOnlyList<ImportJob>> GetExpiredTerminalImportJobsAsync(
        DateTimeOffset terminalBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns terminal export jobs completed before <paramref name="terminalBefore"/>,
    /// oldest first, capped at <paramref name="batchSize"/>.
    /// </summary>
    Task<IReadOnlyList<ExportJob>> GetExpiredTerminalExportJobsAsync(
        DateTimeOffset terminalBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns import jobs stuck in <see cref="ImportJobStatus.Executing"/> whose last
    /// modification (falling back to creation) predates <paramref name="stuckBefore"/>,
    /// oldest first, capped at <paramref name="batchSize"/>.
    /// </summary>
    Task<IReadOnlyList<ImportJob>> GetStuckImportJobsAsync(
        DateTimeOffset stuckBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns export jobs stuck in <see cref="ExportJobStatus.Exporting"/> whose last
    /// modification (falling back to creation) predates <paramref name="stuckBefore"/>,
    /// oldest first, capped at <paramref name="batchSize"/>.
    /// </summary>
    Task<IReadOnlyList<ExportJob>> GetStuckExportJobsAsync(
        DateTimeOffset stuckBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Persists a mutated import job.</summary>
    Task UpdateImportJobAsync(ImportJob job, CancellationToken cancellationToken = default);

    /// <summary>Persists a mutated export job.</summary>
    Task UpdateExportJobAsync(ExportJob job, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes an import job row (record retention expiry).</summary>
    Task DeleteImportJobAsync(ImportJob job, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes an export job row (record retention expiry).</summary>
    Task DeleteExportJobAsync(ExportJob job, CancellationToken cancellationToken = default);
}
