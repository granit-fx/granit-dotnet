using Granit.DataExchange.BackgroundJobs.Options;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Reporting;
using Granit.DataExchange.Retention;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.BackgroundJobs.Services;

/// <summary>
/// GDPR retention sweep for <c>Granit.DataExchange</c> (Art. 5(1)(e) storage limitation):
/// purges expired import/export files, hard-deletes job rows past the record-retention window,
/// and recovers jobs stranded in a non-terminal executing state.
/// </summary>
/// <remarks>
/// Runs as a system job with no ambient tenant — <see cref="IDataExchangeRetentionStore"/>
/// implementations read across every tenant partition (see the cross-tenant contract documented
/// on that interface). Each category is processed independently and per-item: one job's failure
/// (a transient blob-storage error, a concurrency conflict) is logged and skipped rather than
/// aborting the rest of the sweep — the next scheduled run retries it.
/// </remarks>
public sealed partial class RetentionSweepService(
    IDataExchangeRetentionStore store,
    IDataExchangeFileProvider fileProvider,
    TimeProvider timeProvider,
    DataExchangeMetrics metrics,
    IOptions<DataExchangeRetentionOptions> options,
    ILogger<RetentionSweepService> logger)
{
    private const string StuckJobRecoveredErrorCode = "DataExchange:Retention:StuckJobRecovered";
    private const string StuckJobRecoveredMessage = "Stuck job recovered by retention sweep.";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DataExchangeRetentionOptions opts = options.Value;

        int importFilesPurged = await PurgeImportFilesAsync(
            now - opts.ImportFileRetention, opts.SweepBatchSize, now, cancellationToken).ConfigureAwait(false);
        int exportFilesPurged = await PurgeExportFilesAsync(
            now - opts.ExportFileRetention, opts.SweepBatchSize, now, cancellationToken).ConfigureAwait(false);
        int stuckImportsRecovered = await RecoverStuckImportJobsAsync(
            now - opts.StuckJobTimeout, opts.SweepBatchSize, now, cancellationToken).ConfigureAwait(false);
        int stuckExportsRecovered = await RecoverStuckExportJobsAsync(
            now - opts.StuckJobTimeout, opts.SweepBatchSize, now, cancellationToken).ConfigureAwait(false);
        int importRecordsDeleted = await PurgeImportRecordsAsync(
            now - opts.JobRecordRetention, opts.SweepBatchSize, cancellationToken).ConfigureAwait(false);
        int exportRecordsDeleted = await PurgeExportRecordsAsync(
            now - opts.JobRecordRetention, opts.SweepBatchSize, cancellationToken).ConfigureAwait(false);

        Log.SweepCompleted(
            logger,
            importFilesPurged,
            exportFilesPurged,
            stuckImportsRecovered,
            stuckExportsRecovered,
            importRecordsDeleted,
            exportRecordsDeleted);
    }

    private async Task<int> PurgeImportFilesAsync(
        DateTimeOffset cutoff, int batchSize, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<ImportJob> jobs = await store
            .GetImportJobsWithExpiredFilesAsync(cutoff, batchSize, cancellationToken).ConfigureAwait(false);

        List<Guid?> purgedTenants = [];
        foreach (ImportJob job in jobs)
        {
            try
            {
                await fileProvider.DeleteAsync(job.BlobReference, cancellationToken).ConfigureAwait(false);
                job.MarkFileDeleted(now);
                await store.UpdateImportJobAsync(job, cancellationToken).ConfigureAwait(false);
                purgedTenants.Add(job.TenantId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.ItemFailed(logger, "import_file", job.Id, ex);
            }
        }

        RecordMetric("import_file", purgedTenants);
        return purgedTenants.Count;
    }

    private async Task<int> PurgeExportFilesAsync(
        DateTimeOffset cutoff, int batchSize, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExportJob> jobs = await store
            .GetExportJobsWithExpiredFilesAsync(cutoff, batchSize, cancellationToken).ConfigureAwait(false);

        List<Guid?> purgedTenants = [];
        foreach (ExportJob job in jobs)
        {
            try
            {
                // The store query guarantees BlobReference is non-null for these rows.
                await fileProvider.DeleteAsync(job.BlobReference!, cancellationToken).ConfigureAwait(false);
                job.MarkFileDeleted(now);
                await store.UpdateExportJobAsync(job, cancellationToken).ConfigureAwait(false);
                purgedTenants.Add(job.TenantId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.ItemFailed(logger, "export_file", job.Id, ex);
            }
        }

        RecordMetric("export_file", purgedTenants);
        return purgedTenants.Count;
    }

    private async Task<int> RecoverStuckImportJobsAsync(
        DateTimeOffset cutoff, int batchSize, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<ImportJob> jobs = await store
            .GetStuckImportJobsAsync(cutoff, batchSize, cancellationToken).ConfigureAwait(false);

        List<Guid?> recoveredTenants = [];
        foreach (ImportJob job in jobs)
        {
            try
            {
                job.Complete(ImportJobStatus.Failed, BuildStuckRecoveryReport(), now);
                await store.UpdateImportJobAsync(job, cancellationToken).ConfigureAwait(false);
                recoveredTenants.Add(job.TenantId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.ItemFailed(logger, "stuck_import", job.Id, ex);
            }
        }

        RecordMetric("stuck_import", recoveredTenants);
        return recoveredTenants.Count;
    }

    private async Task<int> RecoverStuckExportJobsAsync(
        DateTimeOffset cutoff, int batchSize, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExportJob> jobs = await store
            .GetStuckExportJobsAsync(cutoff, batchSize, cancellationToken).ConfigureAwait(false);

        List<Guid?> recoveredTenants = [];
        foreach (ExportJob job in jobs)
        {
            try
            {
                job.Fail("Recovered by retention sweep: export stuck beyond StuckJobTimeout.", now);
                await store.UpdateExportJobAsync(job, cancellationToken).ConfigureAwait(false);
                recoveredTenants.Add(job.TenantId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.ItemFailed(logger, "stuck_export", job.Id, ex);
            }
        }

        RecordMetric("stuck_export", recoveredTenants);
        return recoveredTenants.Count;
    }

    private async Task<int> PurgeImportRecordsAsync(
        DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
    {
        IReadOnlyList<ImportJob> jobs = await store
            .GetExpiredTerminalImportJobsAsync(cutoff, batchSize, cancellationToken).ConfigureAwait(false);

        List<Guid?> deletedTenants = [];
        foreach (ImportJob job in jobs)
        {
            try
            {
                await store.DeleteImportJobAsync(job, cancellationToken).ConfigureAwait(false);
                deletedTenants.Add(job.TenantId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.ItemFailed(logger, "import_record", job.Id, ex);
            }
        }

        RecordMetric("import_record", deletedTenants);
        return deletedTenants.Count;
    }

    private async Task<int> PurgeExportRecordsAsync(
        DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExportJob> jobs = await store
            .GetExpiredTerminalExportJobsAsync(cutoff, batchSize, cancellationToken).ConfigureAwait(false);

        List<Guid?> deletedTenants = [];
        foreach (ExportJob job in jobs)
        {
            try
            {
                await store.DeleteExportJobAsync(job, cancellationToken).ConfigureAwait(false);
                deletedTenants.Add(job.TenantId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.ItemFailed(logger, "export_record", job.Id, ex);
            }
        }

        RecordMetric("export_record", deletedTenants);
        return deletedTenants.Count;
    }

    /// <summary>
    /// Builds a minimal <see cref="ImportReport"/> for a stuck import job that the sweep force-fails.
    /// No rows were actually processed — the counters are all zero and the single
    /// <see cref="ImportRowError"/> documents the recovery rather than a real row failure.
    /// </summary>
    private static ImportReport BuildStuckRecoveryReport() => new()
    {
        TotalRows = 0,
        SucceededRows = 0,
        FailedRows = 0,
        SkippedRows = 0,
        InsertedRows = 0,
        UpdatedRows = 0,
        Duration = TimeSpan.Zero,
        FinalStatus = ImportJobStatus.Failed,
        RowErrors = [new ImportRowError(0, ImportRowErrorKind.Persistence, [StuckJobRecoveredErrorCode], StuckJobRecoveredMessage)],
    };

    private void RecordMetric(string kind, IReadOnlyList<Guid?> tenantIds)
    {
        foreach (IGrouping<Guid?, Guid?> group in tenantIds.GroupBy(t => t))
        {
            metrics.RecordRetentionPurged(kind, group.Key?.ToString(), group.Count());
        }
    }

    private static partial class Log
    {
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Data exchange retention sweep failed to process {Kind} job {JobId} — skipping, will retry next run")]
        public static partial void ItemFailed(ILogger logger, string kind, Guid jobId, Exception exception);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Data exchange retention sweep completed: {ImportFilesPurged} import file(s) purged, "
                + "{ExportFilesPurged} export file(s) purged, {StuckImportsRecovered} stuck import job(s) recovered, "
                + "{StuckExportsRecovered} stuck export job(s) recovered, {ImportRecordsDeleted} import record(s) deleted, "
                + "{ExportRecordsDeleted} export record(s) deleted")]
        public static partial void SweepCompleted(
            ILogger logger,
            int importFilesPurged,
            int exportFilesPurged,
            int stuckImportsRecovered,
            int stuckExportsRecovered,
            int importRecordsDeleted,
            int exportRecordsDeleted);
    }
}
