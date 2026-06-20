namespace Granit.AI.Chat.Mentions;

/// <summary>
/// Backs the <c>@</c> picker: searches the opted-in <see cref="IAIMentionResolver"/> for entities
/// the caller may mention. Each resolver is queried under the caller's ACLs; results are merged and
/// capped. Unknown or unsearchable types contribute nothing — never an error (ADR-067).
/// </summary>
public interface IAIMentionSearchService
{
    /// <summary>
    /// Searches mentionable entities matching <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The user's free-text query.</param>
    /// <param name="type">
    /// When set, restricts the search to that single mention type; when <see langword="null"/>,
    /// every opted-in resolver is queried and the results merged.
    /// </param>
    /// <param name="limit">The maximum number of suggestions to return across all types.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Up to <paramref name="limit"/> suggestions; empty when nothing matches.</returns>
    ValueTask<IReadOnlyList<AIMentionSuggestion>> SearchAsync(
        string query, string? type = null, int limit = 8, CancellationToken cancellationToken = default);
}
