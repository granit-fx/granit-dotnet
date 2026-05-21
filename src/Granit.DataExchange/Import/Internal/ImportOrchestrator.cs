using System.Diagnostics;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Execution;
using Granit.DataExchange.Import.Identity;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Messages;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Granit.DataExchange.Import.Validation;
using Granit.Events;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.Import.Internal;

/// <summary>
/// Default implementation of <see cref="IImportOrchestrator"/>.
/// Coordinates the full pipeline: Load Job -> Parse -> Map -> Validate -> Resolve Identity -> Execute.
/// </summary>
internal sealed class ImportOrchestrator(
    IImportJobReader jobReader,
    IImportJobWriter jobWriter,
    IDataExchangeFileProvider fileProvider,
    IServiceProvider serviceProvider,
    IClock clock,
    ILocalEventBus eventBus,
    DataExchangeMetrics metrics,
    IOptions<ImportOptions> options) : IImportOrchestrator
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

        var stopwatch = Stopwatch.StartNew();

        try
        {
            ImportReport report = await ExecuteTypedPipelineAsync(job, dryRun: false, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            job.Complete(report.FinalStatus, report, clock.Now);
            await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

            metrics.RecordImportCompleted(report, job);

            await eventBus.PublishAsync(new ImportJobCompletedEto(
                importJobId, job.DefinitionName, report.FinalStatus, job.CreatedBy,
                report.TotalRows, report.SucceededRows, report.FailedRows,
                report.InsertedRows, report.UpdatedRows, report.SkippedRows), cancellationToken).ConfigureAwait(false);

            return report;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
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
                        ex.Message.Length > 500 ? string.Concat(ex.Message.AsSpan(0, 500), "... [truncated]") : ex.Message),
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

        return await ExecuteTypedPipelineAsync(job, dryRun: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImportReport> ExecuteTypedPipelineAsync(ImportJob job, bool dryRun, CancellationToken cancellationToken)
    {
        // Resolve the file parser for this MIME type
        IEnumerable<IFileParser> parsers = serviceProvider.GetServices<IFileParser>();
        IFileParser? parser = parsers.FirstOrDefault(p => p.CanParse(job.MimeType));
        if (parser is null)
        {
            string registered = parsers.Any()
                ? string.Join(", ", parsers.Select(p => p.GetType().Name))
                : "none";
            throw new InvalidOperationException(
                $"No IFileParser registered for MIME type '{job.MimeType}'. " +
                $"Registered parsers: [{registered}]. " +
                $"Ensure the corresponding module is added: GranitDataExchangeCsvModule for CSV, GranitDataExchangeExcelModule for Excel.");
        }

        // Confirmed column mappings — set by ConfirmMappings before the job is queued.
        if (job.Mappings is null || job.Mappings.Count == 0)
        {
            throw new InvalidOperationException(
                $"Import job '{job.Id}' has no confirmed mappings.");
        }

        IReadOnlyList<ImportColumnMapping> mappings = job.Mappings;

        // Open the file stream
        await using Stream fileStream = await fileProvider.OpenAsync(job.BlobReference, cancellationToken).ConfigureAwait(false);
        FileParsingOptions parsingOptions = new() { MimeType = job.MimeType };

        // Build and execute the typed pipeline via reflection
        // The entity type is stored as a string — use the registered ImportDefinition to find it
        Type? executorType = FindExecutorType(job.EntityTypeName);
        if (executorType is null)
        {
            throw new InvalidOperationException(
                $"No IImportExecutor registered for entity type '{job.EntityTypeName}'.");
        }

        return await ExecuteWithReflectionAsync(
            executorType, parser, fileStream, parsingOptions, mappings, dryRun, cancellationToken).ConfigureAwait(false);
    }

    private Type? FindExecutorType(string entityTypeName) =>
        GetRegisteredEntityTypes()
            .FirstOrDefault(entityType => entityType.Name == entityTypeName);

    private IEnumerable<Type> GetRegisteredEntityTypes()
    {
        // Try to resolve ImportDefinition<T> for known entity types
        // The definitions are registered as singletons — enumerate them
        IEnumerable<object> definitions = serviceProvider.GetServices<object>()
            .Where(s => s.GetType().BaseType?.IsGenericType == true
                        && s.GetType().BaseType?.GetGenericTypeDefinition() == typeof(ImportDefinition<>));

        foreach (object definition in definitions)
        {
            Type? entityType = definition.GetType().BaseType?.GetGenericArguments()[0];
            if (entityType is not null)
            {
                yield return entityType;
            }
        }
    }

    private async Task<ImportReport> ExecuteWithReflectionAsync(
        Type entityType,
        IFileParser parser,
        Stream fileStream,
        FileParsingOptions parsingOptions,
        IReadOnlyList<ImportColumnMapping> mappings,
        bool dryRun,
        CancellationToken cancellationToken)
    {
#pragma warning disable S3011 // Reflection on private member — needed to invoke generic method with runtime Type
        System.Reflection.MethodInfo pipelineMethod = typeof(ImportOrchestrator)
            .GetMethod(nameof(RunTypedPipelineAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(entityType);
#pragma warning restore S3011

        var task = pipelineMethod.Invoke(
            this,
            [parser, fileStream, parsingOptions, mappings, dryRun, cancellationToken]) as Task<ImportReport>;

        return await task!.ConfigureAwait(false);
    }

    private async Task<ImportReport> RunTypedPipelineAsync<TEntity>(
        IFileParser parser,
        Stream fileStream,
        FileParsingOptions parsingOptions,
        IReadOnlyList<ImportColumnMapping> mappings,
        bool dryRun,
        CancellationToken cancellationToken) where TEntity : class
    {
        IImportExecutor<TEntity> executor = serviceProvider.GetRequiredService<IImportExecutor<TEntity>>();

        ImportExecutionOptions executionOptions = new()
        {
            BatchSize = options.Value.DefaultBatchSize,
            DryRun = dryRun,
        };

        // Build the streaming pipeline
        IAsyncEnumerable<ValidatedRow<TEntity>> pipeline = BuildPipeline<TEntity>(
            parser, fileStream, parsingOptions, mappings, cancellationToken);

        return await executor.ExecuteAsync(pipeline, executionOptions, null, cancellationToken).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<ValidatedRow<TEntity>> BuildPipeline<TEntity>(
        IFileParser parser,
        Stream fileStream,
        FileParsingOptions parsingOptions,
        IReadOnlyList<ImportColumnMapping> mappings,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) where TEntity : class
    {
        IRecordIdentityResolver<TEntity>? identityResolver = serviceProvider.GetService<IRecordIdentityResolver<TEntity>>();
        IDataMapper<TEntity>? dataMapper = serviceProvider.GetService<IDataMapper<TEntity>>();
        IRowValidator<TEntity>? rowValidator = serviceProvider.GetService<IRowValidator<TEntity>>();
        ImportOptions importOptions = options.Value;

        await foreach (RawImportRow row in parser.ParseAsync(fileStream, parsingOptions, cancellationToken))
        {
            // Map raw row to entity
            if (dataMapper is null)
            {
                continue;
            }

            MappingResult<TEntity> mappingResult = await dataMapper.MapAsync(row, mappings, importOptions, cancellationToken).ConfigureAwait(false);
            if (!mappingResult.Succeeded || mappingResult.Entity is null)
            {
                continue;
            }

            TEntity entity = mappingResult.Entity;

            // Validate
            if (rowValidator is not null)
            {
                RowValidationResult validationResult = await rowValidator.ValidateAsync(entity, row.RowNumber, cancellationToken).ConfigureAwait(false);
                if (!validationResult.IsValid)
                {
                    continue;
                }
            }

            // Resolve identity
            RecordIdentity<TEntity>? identity = null;
            if (identityResolver is not null)
            {
                identity = await identityResolver.ResolveAsync(entity, cancellationToken).ConfigureAwait(false);
            }

            yield return new ValidatedRow<TEntity>(row.RowNumber, entity, identity);
        }
    }
}
