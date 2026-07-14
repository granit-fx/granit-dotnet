using Granit.DataExchange.Import.Reporting;

namespace Granit.DataExchange.Import.Pipeline;

/// <summary>
/// A fully-typed, ready-to-run import pipeline for a single definition:
/// Parse → Map → Validate → Resolve Identity → Execute.
/// </summary>
/// <remarks>
/// Created per execution by <see cref="IImportPipelineDescriptor.Create"/> from a scoped
/// service provider. Errors are carried as data (<see cref="Execution.RowOutcome{TEntity}"/>) —
/// a bad row never aborts the run and always surfaces in the report.
/// </remarks>
public interface IImportPipeline
{
    /// <summary>
    /// The import definition name this pipeline executes.
    /// </summary>
    string DefinitionName { get; }

    /// <summary>
    /// The target entity CLR type.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Runs the pipeline over the given file stream and returns the final report.
    /// </summary>
    /// <param name="context">The execution context (stream, mappings, options).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import report with statistics and row-level errors.</returns>
    Task<ImportReport> ExecuteAsync(ImportPipelineContext context, CancellationToken cancellationToken = default);
}
