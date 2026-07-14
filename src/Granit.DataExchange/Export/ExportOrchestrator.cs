using System.Diagnostics;
using Granit.Commands;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Export.Events;
using Granit.DataExchange.Export.Exceptions;
using Granit.DataExchange.Export.Messages;
using Granit.DataExchange.Export.Pipeline;
using Granit.Domain.ValueObjects;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.Export;

/// <summary>
/// Default <see cref="IExportOrchestrator"/> implementation.
/// Resolves the typed export pipeline from the <see cref="IExportPipelineRegistry"/>,
/// delegates streaming and value extraction to it, and writes the output via the
/// matching <see cref="IExportWriter"/>.
/// </summary>
public sealed partial class ExportOrchestrator(
    IServiceProvider serviceProvider,
    IEnumerable<IExportWriter> writers,
    IExportJobReader jobReader,
    IExportJobWriter jobWriter,
    ICommandSender commandSender,
    IDataExchangeFileProvider fileProvider,
    IClock clock,
    IGuidGenerator guidGenerator,
    ILocalEventBus eventBus,
    IDistributedEventBus distributedEventBus,
    ICurrentTenant currentTenant,
    IExportPipelineRegistry pipelineRegistry,
    IExtraExportFieldProvider extraFieldProvider,
    DataExchangeMetrics metrics,
    ILogger<ExportOrchestrator> logger) : IExportOrchestrator
{
    /// <inheritdoc/>
    public async Task<ExportJobResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken = default)
    {
        _ = serviceProvider.GetRequiredService<IOptions<ExportOptions>>().Value; // Validated early; will carry threshold logic in a future version.
        // Validate early
        IExportDefinitionDescriptor definition = ResolvePipelineDescriptor(request.DefinitionName).Definition;
        IExportWriter writer = ResolveWriter(request.Format);

        // Fast-fail when the format cannot handle complex fields (Throw policy)
        if (definition.HasComplexFields
            && !writer.Capabilities.SupportsHierarchy
            && definition.OnIncompatibleField is OnIncompatibleFieldPolicy.Throw)
        {
            int complexCount = definition.GetFields().Count(f => f.RequiresHierarchy);
            throw new ExportProviderIncompatibleException(request.Format, complexCount);
        }

        // Create the job entity
        var job = ExportJob.Create(
            guidGenerator.Create(),
            request.DefinitionName,
            request.Format,
            request,
            currentTenant.IsAvailable ? currentTenant.Id : null);

        await jobWriter.CreateAsync(job, cancellationToken).ConfigureAwait(false);

        // Dispatch for asynchronous execution via the configured ICommandSender provider.
        await commandSender.SendAsync(new ExecuteExportCommand(job.Id), cancellationToken).ConfigureAwait(false);
        LogExportQueued(job.Id, request.DefinitionName, request.Format);

        return new ExportJobResult(job.Id, ExportJobStatus.Queued);
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        ExportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            LogJobNotFound(jobId);
            return;
        }

        using Activity? activity = DataExchangeActivitySource.Source.StartActivity(
            DataExchangeActivitySource.ExportExecute);
        activity?.SetTag("data_exchange.definition", job.DefinitionName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            job.MarkAsExporting();
            await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

            ExportRequest request = job.Request;
            IExportPipelineDescriptor pipelineDescriptor = ResolvePipelineDescriptor(request.DefinitionName);
            IExportDefinitionDescriptor definition = pipelineDescriptor.Definition;
            IExportWriter writer = ResolveWriter(request.Format);
            IReadOnlyList<ExportFieldDescriptor> fields = ResolveFields(definition, request.SelectedFields, request.IncludeIdForImport);

            // Apply Skip policy: strip complex fields when writer cannot handle them
            fields = FilterIncompatibleFields(definition, writer, fields, request.Format);

            // Stream entities through the typed pipeline into a temporary stream
            IExportPipeline pipeline = pipelineDescriptor.Create(serviceProvider);
            ExportPipelineContext pipelineContext = new()
            {
                Request = request,
                Fields = fields,
                Writer = writer,
            };

            // Store the generated file, streaming pipeline output directly into the provider's
            // destination stream — no full in-memory buffer of the generated file.
            string fileName = $"{SanitizeFileName(request.DefinitionName)}_{clock.Now:yyyy-MM-dd_HHmmss}{writer.FileExtension}";
            long writtenRows = 0;
            BlobReference blobReference = await fileProvider.SaveAsync(
                fileName,
                writer.MimeType,
                async (stream, ct) => writtenRows = await pipeline.WriteAsync(pipelineContext, stream, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
            int rowCount = checked((int)writtenRows);

            stopwatch.Stop();
            job.Complete(blobReference, fileName, rowCount, clock.Now);
            await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

            metrics.RecordExportCompleted(
                request.DefinitionName, request.Format, rowCount,
                job.TenantId?.ToString(), stopwatch.Elapsed);

            await eventBus.PublishAsync(new ExportJobCompletedEto(
                jobId, request.DefinitionName, ExportJobStatus.Completed,
                job.CreatedBy, rowCount, ErrorMessage: null), cancellationToken).ConfigureAwait(false);

            LogExportCompleted(jobId, request.DefinitionName, rowCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            string sanitizedError = ex.Message.Length > 500
                ? $"{ex.Message.AsSpan(0, 500)}… [truncated]"
                : ex.Message;
            job.Fail(sanitizedError, clock.Now);
            await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

            metrics.RecordExportFailed(
                job.DefinitionName, job.Format, job.TenantId?.ToString(), stopwatch.Elapsed);

            await eventBus.PublishAsync(new ExportJobCompletedEto(
                jobId, job.DefinitionName, ExportJobStatus.Failed,
                job.CreatedBy, RowCount: null, sanitizedError), cancellationToken).ConfigureAwait(false);

            await distributedEventBus.PublishAsync(new ExportJobFailedEto(
                jobId, job.DefinitionName, sanitizedError), cancellationToken).ConfigureAwait(false);

            LogExportFailed(jobId, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ExportJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<ExportDownload?> GetDownloadAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        ExportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job?.Status is not ExportJobStatus.Completed || job.BlobReference is null || job.FileName is null)
        {
            return null;
        }

        IExportWriter writer = ResolveWriter(job.Format);
        Stream stream = await fileProvider.OpenAsync(job.BlobReference, cancellationToken).ConfigureAwait(false);
        return new ExportDownload(stream, writer.MimeType, job.FileName);
    }

    private IExportPipelineDescriptor ResolvePipelineDescriptor(string definitionName)
    {
        IExportPipelineDescriptor? descriptor = pipelineRegistry.Find(definitionName);
        if (descriptor is not null)
        {
            return descriptor;
        }

        IReadOnlyList<IExportPipelineDescriptor> registered = pipelineRegistry.GetAll();
        string registeredNames = registered.Count > 0
            ? string.Join(", ", registered.Select(d => d.DefinitionName))
            : "none";
        throw new InvalidOperationException(
            $"Export definition '{definitionName}' not found (lookups are case-sensitive). " +
            $"Registered definitions: [{registeredNames}].");
    }

    private IExportWriter ResolveWriter(string format)
    {
        IExportWriter? writer = writers.FirstOrDefault(w => w.CanWrite(format));
        if (writer is not null)
        {
            return writer;
        }

        string registered = writers.Any()
            ? string.Join(", ", writers.Select(w => w.GetType().Name))
            : "none";
        throw new InvalidOperationException(
            $"No export writer registered for format '{format}'. " +
            $"Registered writers: [{registered}]. " +
            "Ensure the corresponding module is added: GranitDataExchangeCsvModule for 'csv', GranitDataExchangeExcelModule for 'xlsx'.");
    }

    private IReadOnlyList<ExportFieldDescriptor> ResolveFields(
        IExportDefinitionDescriptor definition,
        IReadOnlyList<string>? selectedFields,
        bool includeIdForImport)
    {
        IReadOnlyList<ExportFieldDescriptor> definitionFields = definition.GetFields();

        // Merge extra property fields when IncludeMetadata is enabled
        IReadOnlyList<ExportFieldDescriptor> allFields;
        if (definition.IncludeMetadata)
        {
            IReadOnlyList<ExportFieldDescriptor> extraFields = extraFieldProvider.GetExtraFields(definition.EntityType);
            if (extraFields.Count > 0)
            {
                List<ExportFieldDescriptor> merged = [.. definitionFields, .. extraFields];
                allFields = merged.AsReadOnly();
            }
            else
            {
                allFields = definitionFields;
            }
        }
        else
        {
            allFields = definitionFields;
        }

        if (selectedFields is null or { Count: 0 })
        {
            return PrependIdField(allFields, allFields, includeIdForImport);
        }

        // Filter and reorder based on user selection
        List<ExportFieldDescriptor> result = [];
        foreach (string fieldPath in selectedFields)
        {
            ExportFieldDescriptor? field = allFields.FirstOrDefault(
                f => string.Equals(f.PropertyPath, fieldPath, StringComparison.OrdinalIgnoreCase));

            if (field is not null)
            {
                result.Add(field);
            }
        }

        return PrependIdField(result, allFields, includeIdForImport);
    }

    private static IReadOnlyList<ExportFieldDescriptor> PrependIdField(
        IReadOnlyList<ExportFieldDescriptor> fields,
        IReadOnlyList<ExportFieldDescriptor> allFields,
        bool includeIdForImport)
    {
        if (!includeIdForImport)
        {
            return fields is List<ExportFieldDescriptor> list ? list.AsReadOnly() : fields;
        }

        if (fields.Any(f => string.Equals(f.PropertyPath, "Id", StringComparison.OrdinalIgnoreCase)))
        {
            return fields is List<ExportFieldDescriptor> list ? list.AsReadOnly() : fields;
        }

        ExportFieldDescriptor? idField = allFields.FirstOrDefault(
            f => string.Equals(f.PropertyPath, "Id", StringComparison.OrdinalIgnoreCase));

        if (idField is null)
        {
            return fields is List<ExportFieldDescriptor> list ? list.AsReadOnly() : fields;
        }

        List<ExportFieldDescriptor> withId = [idField, .. fields];
        return withId.AsReadOnly();
    }

    private IReadOnlyList<ExportFieldDescriptor> FilterIncompatibleFields(
        IExportDefinitionDescriptor definition,
        IExportWriter writer,
        IReadOnlyList<ExportFieldDescriptor> fields,
        string format)
    {
        if (!definition.HasComplexFields || writer.Capabilities.SupportsHierarchy)
        {
            return fields;
        }

        // Throw policy: should have already been caught in ExportAsync; double-check at execute time
        // (e.g. if the job was queued before the definition was updated)
        if (definition.OnIncompatibleField is OnIncompatibleFieldPolicy.Throw)
        {
            int complexCount = fields.Count(f => f.RequiresHierarchy);
            if (complexCount > 0)
            {
                throw new ExportProviderIncompatibleException(format, complexCount);
            }

            return fields;
        }

        // Skip policy: drop complex fields and warn
        var filtered = fields.Where(f => !f.RequiresHierarchy).ToList();
        int skippedCount = fields.Count - filtered.Count;
        if (skippedCount > 0)
        {
            LogComplexFieldsSkipped(definition.Name, format, skippedCount);
        }

        return filtered.AsReadOnly();
    }

    private static string SanitizeFileName(string definitionName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(definitionName.Select(c => invalidChars.Contains(c) ? '_' : c));
    }

    [LoggerMessage(1, LogLevel.Information, "Export job {ExportJobId} queued for '{DefinitionName}' format '{Format}'")]
    private partial void LogExportQueued(Guid exportJobId, string definitionName, string format);

    [LoggerMessage(2, LogLevel.Information, "Export job {ExportJobId} completed for '{DefinitionName}' ({RowCount} rows)")]
    private partial void LogExportCompleted(Guid exportJobId, string definitionName, int rowCount);

    [LoggerMessage(3, LogLevel.Error, "Export job {ExportJobId} failed")]
    private partial void LogExportFailed(Guid exportJobId, Exception ex);

    [LoggerMessage(4, LogLevel.Warning, "Export job {ExportJobId} not found")]
    private partial void LogJobNotFound(Guid exportJobId);

    [LoggerMessage(5, LogLevel.Warning,
        "Export definition '{DefinitionName}' has complex fields that are not supported by format '{Format}'. " +
        "{SkippedCount} complex field(s) were skipped (OnIncompatibleField = Skip).")]
    private partial void LogComplexFieldsSkipped(string definitionName, string format, int skippedCount);
}
