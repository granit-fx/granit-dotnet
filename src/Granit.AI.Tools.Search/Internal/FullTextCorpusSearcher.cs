using Granit.Indexing;

namespace Granit.AI.Tools.Search.Internal;

/// <summary>
/// <see cref="ICorpusSearcher"/> backed by a full-text index. Tenant isolation and per-record
/// ACL are applied by <c>ISearchService</c> (its registered <c>ISearchResultAuthorizer&lt;TKey&gt;</c>
/// over-fetch loop), so results are bounded by what the caller may access. The full-text page
/// exposes no per-item score, so snippets carry a <see langword="null"/> score.
/// </summary>
internal sealed class FullTextCorpusSearcher<TKey, TResult>(
    string name,
    string? description,
    Func<TResult, string> textSelector,
    Func<TResult, string>? idSelector,
    ISearchService<TKey, TResult> searchService) : ICorpusSearcher
    where TKey : notnull
{
    public string Name => name;

    public string? Description => description;

    public string Mode => "full_text";

    public async Task<IReadOnlyList<SearchSnippet>> SearchAsync(
        string query, int limit, CancellationToken cancellationToken)
    {
        SearchPage<TResult> page = await searchService
            .SearchAsync(new SearchRequest(query, Page: 1, PageSize: limit), cancellationToken)
            .ConfigureAwait(false);

        return [.. page.Items.Select(item =>
            new SearchSnippet(idSelector?.Invoke(item) ?? string.Empty, textSelector(item), null, "full_text"))];
    }
}
