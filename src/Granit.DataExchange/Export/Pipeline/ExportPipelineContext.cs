namespace Granit.DataExchange.Export.Pipeline;

/// <summary>
/// Execution context handed to an <see cref="IExportPipeline"/> by the orchestrator.
/// </summary>
/// <remarks>
/// The orchestrator resolves the effective field list (user selection, ID prepend,
/// incompatible-field filtering) and the target writer before invoking the pipeline;
/// the pipeline only streams entities, extracts values, and delegates to the writer.
/// </remarks>
public sealed record ExportPipelineContext
{
    /// <summary>
    /// The original export request (definition name, format, filtering, sorting, search).
    /// </summary>
    public required ExportRequest Request { get; init; }

    /// <summary>
    /// The resolved, ordered field descriptors. Each emitted row is an <c>object?[]</c>
    /// whose values are aligned to this list's indexes.
    /// </summary>
    public required IReadOnlyList<ExportFieldDescriptor> Fields { get; init; }

    /// <summary>
    /// The writer matching the requested output format.
    /// </summary>
    public required IExportWriter Writer { get; init; }
}
