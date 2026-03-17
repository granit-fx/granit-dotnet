using System.Runtime.CompilerServices;
using System.Text.Json;
using Granit.Core.Events;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Export.Messages;
using Granit.DataExchange.Import.Pipeline;
using Granit.Guids;
using Granit.Querying;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.Export.Internal;

/// <summary>
/// Default <see cref="IExportOrchestrator"/> implementation.
/// Resolves the export definition, streams data via
/// <see cref="IExportDataSource{TEntity}"/> + optional <see cref="IQueryEngine{TEntity}"/>,
/// extracts field values, and writes the output via the matching <see cref="IExportWriter"/>.
/// </summary>
internal sealed partial class ExportOrchestrator(
    IServiceProvider serviceProvider,
    IEnumerable<IExportWriter> writers,
    IExportJobReader jobReader,
    IExportJobWriter jobWriter,
    IExportCommandDispatcher dispatcher,
    IImportFileProvider fileProvider,
    IClock clock,
    IGuidGenerator guidGenerator,
    ILocalEventBus eventBus,
    ILogger<ExportOrchestrator> logger) : IExportOrchestrator
{
    /// <inheritdoc/>
    public async Task<ExportJobResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken = default)
    {
        _ = serviceProvider.GetRequiredService<IOptions<ExportOptions>>().Value; // Validated early; will carry threshold logic in a future version.
        // Validate early
        _ = ResolveDefinition(request.DefinitionName);
        _ = ResolveWriter(request.Format);

        // Create the job entity
        ExportJob job = new()
        {
            Id = guidGenerator.Create(),
            DefinitionName = request.DefinitionName,
            Format = request.Format,
            RequestJson = JsonSerializer.Serialize(request),
            Status = ExportJobStatus.Queued,
        };

        await jobWriter.CreateAsync(job, cancellationToken).ConfigureAwait(false);

        // Dispatch to background worker
        await dispatcher.DispatchAsync(new ExecuteExportCommand(job.Id), cancellationToken).ConfigureAwait(false);
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

        try
        {
            job.Status = ExportJobStatus.Exporting;
            await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

            ExportRequest request = JsonSerializer.Deserialize<ExportRequest>(job.RequestJson)!;
            IExportDefinitionDescriptor definition = ResolveDefinition(request.DefinitionName);
            IExportWriter writer = ResolveWriter(request.Format);
            IReadOnlyList<ExportFieldDescriptor> fields = ResolveFields(definition, request.SelectedFields);

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
            string blobReference = await fileProvider.SaveAsync(fileName, outputStream, cancellationToken).ConfigureAwait(false);

            job.Status = ExportJobStatus.Completed;
            job.BlobReference = blobReference;
            job.FileName = fileName;
            job.RowCount = rowCount;
            job.CompletedAt = clock.Now;
            await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

            await eventBus.PublishAsync(new ExportJobCompletedEvent(
                jobId, request.DefinitionName, ExportJobStatus.Completed,
                job.CreatedBy, rowCount, ErrorMessage: null), cancellationToken).ConfigureAwait(false);

            LogExportCompleted(jobId, request.DefinitionName, rowCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            job.Status = ExportJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = clock.Now;
            await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

            await eventBus.PublishAsync(new ExportJobCompletedEvent(
                jobId, job.DefinitionName, ExportJobStatus.Failed,
                job.CreatedBy, RowCount: null, ex.Message), cancellationToken).ConfigureAwait(false);

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
        IExportDefinitionDescriptor? descriptor = serviceProvider
            .GetServices<IExportDefinitionDescriptor>()
            .FirstOrDefault(d => string.Equals(d.Name, definitionName, StringComparison.OrdinalIgnoreCase));

        return descriptor ?? throw new InvalidOperationException(
            $"Export definition '{definitionName}' not found.");
    }

    private IExportWriter ResolveWriter(string format)
    {
        IExportWriter? writer = writers.FirstOrDefault(w => w.CanWrite(format));
        return writer ?? throw new InvalidOperationException(
            $"No export writer registered for format '{format}'.");
    }

    private static IReadOnlyList<ExportFieldDescriptor> ResolveFields(
        IExportDefinitionDescriptor definition,
        IReadOnlyList<string>? selectedFields)
    {
        IReadOnlyList<ExportFieldDescriptor> allFields = definition.GetFields();

        if (selectedFields is null or { Count: 0 })
        {
            return allFields;
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

        return result.AsReadOnly();
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

        int count = 0;
        await foreach (object entity in typedIterator.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            count++;
            yield return ExtractRow(entity, fields);
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

    private static Dictionary<string, object?> ExtractRow(
        object entity, IReadOnlyList<ExportFieldDescriptor> fields)
    {
        Dictionary<string, object?> row = new(fields.Count);

        foreach (string propertyPath in fields.Select(field => field.PropertyPath))
        {
            row[propertyPath] = ResolvePropertyValue(entity, propertyPath);
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

    private static string SanitizeFileName(string definitionName) =>
        definitionName.Replace('.', '_').Replace('/', '_').Replace('\\', '_');

    [LoggerMessage(1, LogLevel.Information, "Export job {ExportJobId} queued for '{DefinitionName}' format '{Format}'")]
    private partial void LogExportQueued(Guid exportJobId, string definitionName, string format);

    [LoggerMessage(2, LogLevel.Information, "Export job {ExportJobId} completed for '{DefinitionName}' ({RowCount} rows)")]
    private partial void LogExportCompleted(Guid exportJobId, string definitionName, int rowCount);

    [LoggerMessage(3, LogLevel.Error, "Export job {ExportJobId} failed")]
    private partial void LogExportFailed(Guid exportJobId, Exception ex);

    [LoggerMessage(4, LogLevel.Warning, "Export job {ExportJobId} not found")]
    private partial void LogJobNotFound(Guid exportJobId);
}
