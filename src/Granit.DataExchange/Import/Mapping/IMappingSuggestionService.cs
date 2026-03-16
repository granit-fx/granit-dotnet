namespace Granit.DataExchange.Import.Mapping;

/// <summary>
/// Facade for the 4-tier mapping suggestion pipeline:
/// Saved → Exact → Fuzzy → Semantic (AI).
/// </summary>
/// <remarks>
/// Columns already matched by a higher-confidence tier are excluded from lower tiers.
/// Deduplication keeps the best confidence per source column.
/// </remarks>
public interface IMappingSuggestionService
{
    /// <summary>
    /// Suggests column-to-property mappings for the given file headers.
    /// </summary>
    /// <typeparam name="TEntity">The target entity type (used to resolve the import definition).</typeparam>
    /// <param name="headers">Column headers extracted from the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of mapping suggestions, one per header that could be matched.
    /// Unmatchable columns are excluded.
    /// </returns>
    Task<IReadOnlyList<ImportColumnMapping>> SuggestMappingsAsync<TEntity>(
        IReadOnlyList<string> headers,
        CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// Suggests column-to-property mappings with optional preview rows for the AI tier.
    /// </summary>
    /// <typeparam name="TEntity">The target entity type.</typeparam>
    /// <param name="headers">Column headers extracted from the file.</param>
    /// <param name="previewRows">
    /// Optional preview of the first data rows for AI-assisted mapping.
    /// Each element is one row as an array of cell values, aligned with <paramref name="headers"/>.
    /// <b>GDPR warning</b>: may contain PII. Only provide when safe (see <c>DataExchangeAIOptions.IncludePreviewRows</c>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ImportColumnMapping>> SuggestMappingsAsync<TEntity>(
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]>? previewRows,
        CancellationToken cancellationToken = default) where TEntity : class
        => SuggestMappingsAsync<TEntity>(headers, cancellationToken);
}
