using System.Diagnostics;
using System.Runtime.CompilerServices;
using Granit.Commands;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Export.Events;
using Granit.DataExchange.Export.Exceptions;
using Granit.DataExchange.Export.Messages;
using Granit.Domain.ValueObjects;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.Export;

/// <summary>
/// Default <see cref="IExportOrchestrator"/> implementation.
/// Resolves the export definition, streams data via
/// <see cref="IExportDataSource{TEntity}"/> + optional <see cref="IQueryEngine{TEntity}"/>,
/// extracts field values, and writes the output via the matching <see cref="IExportWriter"/>.
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
    IExtraExportFieldProvider extraFieldProvider,
    IExportExtraValueResolver extraValueResolver,
    DataExchangeMetrics metrics,
    ILogger<ExportOrchestrator> logger) : IExportOrchestrator
{
    /// <inheritdoc/>
    public async Task<ExportJobResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken = default)
    {
        _ = serviceProvider.GetRequiredService<IOptions<ExportOptions>>().Value; // Validated early; will carry threshold logic in a future version.
        // Validate early
        IExportDefinitionDescriptor definition = ResolveDefinition(request.DefinitionName);
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
            IExportDefinitionDescriptor definition = ResolveDefinition(request.DefinitionName);
            IExportWriter writer = ResolveWriter(request.Format);
            IReadOnlyList<ExportFieldDescriptor> fields = ResolveFields(definition, request.SelectedFields, request.IncludeIdForImport);

            // Apply Skip policy: strip complex fields when writer cannot handle them
            fields = FilterIncompatibleFields(definition, writer, fields, request.Format);

            // Project entity rows to flat dictionaries
            int rowCount = 0;
            IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows =
                GetProjectedRows(definition, request, fields, count => rowCount = count, cancellationToken);

            // Write to temporary stream
            MemoryStream outputStream = new();
            await writer.WriteAsync(outputStream, fields, rows, cancellationToken).ConfigureAwait(false);
            outputStream.Position = 0;

            // Store the generated file
            string fileName = $"{SanitizeFileName(request.DefinitionName)}_{clock.Now:yyyy-MM-dd_HHmmss}{writer.FileExtension}";
            BlobReference blobReference = await fileProvider.SaveAsync(fileName, outputStream, cancellationToken).ConfigureAwait(false);

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
                ? string.Concat(ex.Message.AsSpan(0, 500), "… [truncated]")
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

    private IExportDefinitionDescriptor ResolveDefinition(string definitionName)
    {
        IExportDefinitionProvider? provider = serviceProvider.GetService<IExportDefinitionProvider>();
        if (provider is not null)
        {
            IExportDefinitionDescriptor? descriptor = provider.FindByName(definitionName);
            if (descriptor is not null)
            {
                return descriptor;
            }
        }
        else
        {
            // Backward compat: no provider registered (no EF Core module)
            IExportDefinitionDescriptor? descriptor = serviceProvider
                .GetServices<IExportDefinitionDescriptor>()
                .FirstOrDefault(d => string.Equals(d.Name, definitionName, StringComparison.OrdinalIgnoreCase));

            if (descriptor is not null)
            {
                return descriptor;
            }
        }

        throw new InvalidOperationException(
            $"Export definition '{definitionName}' not found.");
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
            $"Ensure the corresponding module is added: GranitDataExchangeCsvModule for 'csv', GranitDataExchangeExcelModule for 'xlsx'.");
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

    private async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> GetProjectedRows(
        IExportDefinitionDescriptor definition,
        ExportRequest request,
        IReadOnlyList<ExportFieldDescriptor> fields,
        Action<int> setRowCount,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Resolve the data source dynamically (single type param)
        Type dataSourceType = typeof(IExportDataSource<>)
            .MakeGenericType(definition.EntityType);
        object dataSource = serviceProvider.GetRequiredService(dataSourceType);

        // Call GetQueryable() via reflection
        System.Reflection.MethodInfo getQueryableMethod = dataSourceType
            .GetMethod(nameof(IExportDataSource<object>.GetQueryable))!;
        object queryable = getQueryableMethod.Invoke(dataSource, [])!;

        IAsyncEnumerable<object> typedIterator;

        if (definition.QueryDefinitionName is not null)
        {
            // Use IQueryEngine for filtering and sorting
            Type queryEngineType = typeof(IQueryEngine<>)
                .MakeGenericType(definition.EntityType);
            object queryEngine = serviceProvider.GetRequiredService(queryEngineType);

            QueryRequest queryRequest = new()
            {
                Sort = request.Sort,
                Filter = request.Filter,
                Presets = request.Presets,
                Search = request.Search,
            };

            // Call ExecuteStreamAsync(queryable, queryRequest, cancellationToken) via reflection
            System.Reflection.MethodInfo streamMethod = queryEngineType
                .GetMethod(nameof(IQueryEngine<object>.ExecuteStreamAsync))!;
            object asyncEnumerable = streamMethod.Invoke(queryEngine, [queryable, queryRequest, cancellationToken])!;

            // Bridge the generic gap via IterateAsync<TEntity>
            System.Reflection.MethodInfo iterateMethod = typeof(ExportOrchestrator)
                .GetMethod(nameof(IterateAsync), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(definition.EntityType);
            typedIterator = (IAsyncEnumerable<object>)iterateMethod.Invoke(null, [asyncEnumerable, cancellationToken])!;
        }
        else
        {
            // No QueryDefinition — stream queryable directly (sync iteration)
            System.Reflection.MethodInfo enumerateMethod = typeof(ExportOrchestrator)
                .GetMethod(nameof(EnumerateQueryable), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(definition.EntityType);
            typedIterator = (IAsyncEnumerable<object>)enumerateMethod.Invoke(null, [queryable, cancellationToken])!;
        }

        // Build set of extra property names for fast lookup during row extraction
        HashSet<string>? extraPropertyNames = null;
        if (definition.IncludeMetadata)
        {
            IReadOnlyList<ExportFieldDescriptor> extraFields = extraFieldProvider.GetExtraFields(definition.EntityType);
            if (extraFields.Count > 0)
            {
                extraPropertyNames = new HashSet<string>(
                    extraFields.Select(f => f.PropertyPath), StringComparer.Ordinal);
            }
        }

        int count = 0;
        await foreach (object entity in typedIterator.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            count++;
            yield return ExtractRow(entity, fields, extraPropertyNames);
        }

        setRowCount(count);
    }

    /// <summary>
    /// Iterates an <c>IAsyncEnumerable&lt;T&gt;</c>, yielding each element as <c>object</c>.
    /// Called via reflection from <see cref="GetProjectedRows"/> to bridge the generic gap.
    /// Public on an internal class to avoid <c>BindingFlags.NonPublic</c> in reflection (S3011).
    /// </summary>
    public static async IAsyncEnumerable<object> IterateAsync<T>(
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken) where T : notnull
    {
        await foreach (T item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Enumerates an <c>IQueryable&lt;T&gt;</c> synchronously, yielding each element as <c>object</c>.
    /// Used when no <c>QueryDefinition</c> is associated with the export definition.
    /// Public on an internal class to avoid <c>BindingFlags.NonPublic</c> in reflection (S3011).
    /// </summary>
    public static async IAsyncEnumerable<object> EnumerateQueryable<T>(
        IQueryable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken) where T : notnull
    {
        await Task.CompletedTask.ConfigureAwait(false);
        foreach (T item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    private Dictionary<string, object?> ExtractRow(
        object entity,
        IReadOnlyList<ExportFieldDescriptor> fields,
        HashSet<string>? extraPropertyNames)
    {
        Dictionary<string, object?> row = new(fields.Count);

        foreach (ExportFieldDescriptor field in fields)
        {
            string propertyPath = field.PropertyPath;
            object? value;

            if (field.ValueSelector is not null)
            {
                value = field.ValueSelector(entity);
            }
            else if (extraPropertyNames is not null && extraPropertyNames.Contains(propertyPath))
            {
                value = extraValueResolver.ResolveExtraValue(entity, propertyPath);
            }
            else
            {
                value = ResolvePropertyValue(entity, propertyPath);
            }

            row[propertyPath] = value;
        }

        return row;
    }

    private static object? ResolvePropertyValue(object? obj, string propertyPath)
    {
        if (obj is null)
        {
            return null;
        }

        ReadOnlySpan<char> remaining = propertyPath.AsSpan();
        object? current = obj;

        while (!remaining.IsEmpty && current is not null)
        {
            int dotIndex = remaining.IndexOf('.');
            ReadOnlySpan<char> segment = dotIndex >= 0 ? remaining[..dotIndex] : remaining;
            remaining = dotIndex >= 0 ? remaining[(dotIndex + 1)..] : [];

            System.Reflection.PropertyInfo? prop = current.GetType().GetProperty(segment.ToString());
            current = prop?.GetValue(current);
        }

        return current;
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
