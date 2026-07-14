using Granit.DataExchange.Import.Execution;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Reporting;

namespace Granit.DataExchange.Import.Pipeline;

/// <summary>
/// Everything an <see cref="IImportPipeline"/> needs to run one import execution.
/// </summary>
public sealed record ImportPipelineContext
{
    /// <summary>
    /// The uploaded file stream to parse. Owned by the caller — the pipeline does not dispose it.
    /// </summary>
    public required Stream FileStream { get; init; }

    /// <summary>
    /// MIME type of the file, used to dispatch the matching <see cref="Parsing.IFileParser"/>.
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// The user-confirmed column-to-property mappings.
    /// </summary>
    public required IReadOnlyList<ImportColumnMapping> Mappings { get; init; }

    /// <summary>
    /// Execution options (batch size, dry-run, error behavior).
    /// </summary>
    public required ImportExecutionOptions ExecutionOptions { get; init; }

    /// <summary>
    /// Optional progress reporter forwarded to the executor.
    /// </summary>
    public IProgress<ImportProgress>? Progress { get; init; }
}
