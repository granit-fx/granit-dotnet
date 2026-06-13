namespace Granit.AI.Tools.Search.Internal;

/// <summary>
/// Non-generic seam over a single searchable corpus, isolating the generic full-text
/// <c>ISearchService&lt;TKey, TResult&gt;</c> from the (non-generic) <c>search</c> tool.
/// </summary>
internal interface ICorpusSearcher
{
    /// <summary>The corpus name (forms the tool name <c>search_{Name}</c>).</summary>
    string Name { get; }

    /// <summary>Optional model-facing description.</summary>
    string? Description { get; }

    /// <summary>The retrieval mode: <c>semantic</c> or <c>full_text</c>.</summary>
    string Mode { get; }

    /// <summary>Runs the search and returns ranked snippets within the caller's scope.</summary>
    Task<IReadOnlyList<SearchSnippet>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
}
