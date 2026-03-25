using Granit.QueryEngine.Meta;

namespace Granit.QueryEngine.AI;

/// <summary>
/// Translates natural language queries into structured <see cref="QueryRequest"/> objects.
/// </summary>
public interface INaturalLanguageQueryTranslator
{
    /// <summary>
    /// Translates a natural language phrase into a <see cref="QueryRequest"/> using the query metadata
    /// as schema context for the LLM.
    /// </summary>
    /// <param name="naturalLanguage">User's query in natural language.</param>
    /// <param name="metadata">Query metadata describing available columns, filters, sorts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A structured <see cref="QueryRequest"/>, or <c>null</c> if the phrase cannot be translated.</returns>
    Task<QueryRequest?> TranslateAsync(
        string naturalLanguage,
        QueryMetadata metadata,
        CancellationToken cancellationToken = default);
}
