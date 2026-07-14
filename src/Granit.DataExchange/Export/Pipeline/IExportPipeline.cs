namespace Granit.DataExchange.Export.Pipeline;

/// <summary>
/// A fully typed export pipeline bound to a single export definition.
/// </summary>
/// <remarks>
/// <para>
/// Pipelines are created per execution scope by
/// <see cref="IExportPipelineDescriptor.Create(IServiceProvider)"/> so they can capture scoped
/// services (data source, query engine, extra-value resolver). The entity type is closed at
/// registration time, so streaming and value extraction run without any reflection:
/// field values are read through compiled delegates and rows are emitted as ordered
/// <c>object?[]</c> arrays aligned to the field list.
/// </para>
/// </remarks>
public interface IExportPipeline
{
    /// <summary>
    /// The name of the export definition this pipeline executes.
    /// </summary>
    string DefinitionName { get; }

    /// <summary>
    /// The source entity CLR type.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// The export definition descriptor backing this pipeline.
    /// </summary>
    IExportDefinitionDescriptor Definition { get; }

    /// <summary>
    /// Streams the export data and writes it to <paramref name="output"/> via the writer
    /// carried by the context.
    /// </summary>
    /// <param name="context">The resolved execution context (request, fields, writer).</param>
    /// <param name="output">The target stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of data rows written.</returns>
    Task<long> WriteAsync(
        ExportPipelineContext context,
        Stream output,
        CancellationToken cancellationToken = default);
}
