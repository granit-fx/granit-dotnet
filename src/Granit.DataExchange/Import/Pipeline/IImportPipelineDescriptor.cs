using Granit.DataExchange.Import.Mapping;

namespace Granit.DataExchange.Import.Pipeline;

/// <summary>
/// Descriptor for a typed import pipeline. Knows the entity type statically and acts as the
/// single dispatch point for all per-definition operations that need that type
/// (pipeline construction, mapping suggestions) — no reflection anywhere.
/// </summary>
public interface IImportPipelineDescriptor
{
    /// <summary>
    /// The import definition name (e.g. <c>"Acme.PatientImport"</c>).
    /// </summary>
    string DefinitionName { get; }

    /// <summary>
    /// The target entity CLR type.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// The non-generic view of the underlying import definition
    /// (constraints, field metadata).
    /// </summary>
    IImportDefinitionDescriptor Definition { get; }

    /// <summary>
    /// Creates a pipeline instance bound to the given scoped provider.
    /// </summary>
    /// <param name="scopedProvider">
    /// The scoped service provider used to resolve parsers, executor, validator,
    /// identity resolver, and options.
    /// </param>
    /// <returns>A ready-to-run pipeline.</returns>
    IImportPipeline Create(IServiceProvider scopedProvider);

    /// <summary>
    /// Suggests column-to-property mappings for the given file headers via the
    /// scoped <see cref="IMappingSuggestionService"/>, with the entity type closed statically.
    /// </summary>
    /// <param name="scopedProvider">The scoped service provider.</param>
    /// <param name="headers">Column headers extracted from the file.</param>
    /// <param name="previewRows">
    /// Optional preview of the first data rows for the AI tier
    /// (see <see cref="IMappingSuggestionService"/> for the GDPR caveat).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The suggested mappings, one per matchable header.</returns>
    Task<IReadOnlyList<ImportColumnMapping>> SuggestMappingsAsync(
        IServiceProvider scopedProvider,
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]>? previewRows,
        CancellationToken cancellationToken);
}
