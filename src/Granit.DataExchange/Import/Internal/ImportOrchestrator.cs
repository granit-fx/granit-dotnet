using System.Diagnostics;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Execution;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Messages;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Granit.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.Import.Internal;

/// <summary>
/// Default implementation of <see cref="IImportOrchestrator"/>.
/// Loads the job, resolves the typed pipeline from <see cref="IImportPipelineRegistry"/>,
/// and delegates execution: Parse → Map → Validate → Resolve Identity → Execute.
/// </summary>
internal sealed partial class ImportOrchestrator(
    IImportJobReader jobReader,
    IImportJobWriter jobWriter,
    IDataExchangeFileProvider fileProvider,
    IImportPipelineRegistry pipelineRegistry,
    IServiceProvider serviceProvider,
    IClock clock,
    ILocalEventBus eventBus,
    DataExchangeMetrics metrics,
    IOptions<ImportOptions> options,
    ILogger<ImportOrchestrator> logger) : IImportOrchestrator
{
    /// <inheritdoc/>
    public async Task<ImportReport> ExecuteAsync(Guid importJobId, CancellationToken cancellationToken = default)
    {
        ImportJob? job = await jobReader.GetAsync(importJobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            throw new InvalidOperationException($"Import job '{importJobId}' not found.");
        }

        job.MarkAsExecuting();
        await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

        using Activity? activity = DataExchangeActivitySource.Source.StartActivity(
            DataExchangeActivitySource.ImportExecute);
        activity?.SetTag("data_exchange.definition", job.DefinitionName);

        LogImportStarted(importJobId, job.DefinitionName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            ImportReport report = await RunPipelineAsync(job, dryRun: false, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            job.Complete(report.FinalStatus, report, clock.Now);
            await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

            metrics.RecordImportCompleted(report, job);

            await eventBus.PublishAsync(new ImportJobCompletedEto(
                importJobId, job.DefinitionName, report.FinalStatus, job.CreatedBy,
                report.TotalRows, report.SucceededRows, report.FailedRows,
                report.InsertedRows, report.UpdatedRows, report.SkippedRows), cancellationToken).ConfigureAwait(false);

            LogImportCompleted(importJobId, job.DefinitionName, report.TotalRows, report.SucceededRows, report.FailedRows);
            return report;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogImportFailed(importJobId, job.DefinitionName, ex);

            ImportReport errorReport = new()
            {
                TotalRows = 0,
                SucceededRows = 0,
                FailedRows = 0,
                SkippedRows = 0,
                InsertedRows = 0,
                UpdatedRows = 0,
                Duration = stopwatch.Elapsed,
                FinalStatus = ImportJobStatus.Failed,
                RowErrors =
                [
                    new ImportRowError(0, ImportRowErrorKind.Persistence, ["Granit:DataExchange:PipelineError"],
                        ex.Message.Length > 500 ? $"{ex.Message.AsSpan(0, 500)}... [truncated]" : ex.Message),
                ],
            };

            job.Complete(ImportJobStatus.Failed, errorReport, clock.Now);
            await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

            metrics.RecordImportCompleted(errorReport, job);

            await eventBus.PublishAsync(new ImportJobCompletedEto(
                importJobId, job.DefinitionName, ImportJobStatus.Failed, job.CreatedBy,
                errorReport.TotalRows, errorReport.SucceededRows, errorReport.FailedRows,
                errorReport.InsertedRows, errorReport.UpdatedRows, errorReport.SkippedRows), cancellationToken).ConfigureAwait(false);

            return errorReport;
        }
    }

    /// <inheritdoc/>
    public async Task<ImportReport> DryRunAsync(Guid importJobId, CancellationToken cancellationToken = default)
    {
        ImportJob? job = await jobReader.GetAsync(importJobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            throw new InvalidOperationException($"Import job '{importJobId}' not found.");
        }

        LogDryRunStarted(importJobId, job.DefinitionName);
        return await RunPipelineAsync(job, dryRun: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImportReport> RunPipelineAsync(ImportJob job, bool dryRun, CancellationToken cancellationToken)
    {
        IImportPipelineDescriptor? descriptor = pipelineRegistry.Find(job.DefinitionName);
        if (descriptor is null)
        {
            IReadOnlyList<IImportPipelineDescriptor> registered = pipelineRegistry.GetAll();
            string names = registered.Count > 0
                ? string.Join(", ", registered.Select(d => d.DefinitionName))
                : "none";
            throw new InvalidOperationException(
                $"No import definition named '{job.DefinitionName}' is registered (lookup is case-sensitive). " +
                $"Registered definitions: [{names}]. " +
                "Register one with services.AddImportDefinition<TEntity, TDefinition>().");
        }

        // Confirmed column mappings — set by ConfirmMappings before the job is queued.
        if (job.Mappings is null || job.Mappings.Count == 0)
        {
            throw new InvalidOperationException(
                $"Import job '{job.Id}' has no confirmed mappings.");
        }

        IReadOnlyList<ImportColumnMapping> mappings = job.Mappings;

        await using Stream fileStream = await fileProvider.OpenAsync(job.BlobReference, cancellationToken).ConfigureAwait(false);

        ImportPipelineContext context = new()
        {
            FileStream = fileStream,
            MimeType = job.MimeType,
            Mappings = mappings,
            ExecutionOptions = new ImportExecutionOptions
            {
                BatchSize = options.Value.DefaultBatchSize,
                DryRun = dryRun,
            },
        };

        IImportPipeline pipeline = descriptor.Create(serviceProvider);
        return await pipeline.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(1, LogLevel.Information, "Import job {ImportJobId} started for '{DefinitionName}'")]
    private partial void LogImportStarted(Guid importJobId, string definitionName);

    [LoggerMessage(2, LogLevel.Information, "Import job {ImportJobId} completed for '{DefinitionName}' ({TotalRows} rows: {SucceededRows} succeeded, {FailedRows} failed)")]
    private partial void LogImportCompleted(Guid importJobId, string definitionName, int totalRows, int succeededRows, int failedRows);

    [LoggerMessage(3, LogLevel.Error, "Import job {ImportJobId} failed for '{DefinitionName}'")]
    private partial void LogImportFailed(Guid importJobId, string definitionName, Exception ex);

    [LoggerMessage(4, LogLevel.Information, "Import job {ImportJobId} dry-run started for '{DefinitionName}'")]
    private partial void LogDryRunStarted(Guid importJobId, string definitionName);
}
