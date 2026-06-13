using Granit.AI.VectorData;

namespace Granit.AI.Tools.Search.Internal;

/// <summary>
/// <see cref="ICorpusSearcher"/> backed by a configured semantic collection. Results carry
/// similarity scores. Scoping is the collection's (tenant-scoped by the vector provider).
/// </summary>
internal sealed class SemanticCorpusSearcher(
    string name,
    string? description,
    string collectionName,
    ISemanticSearchService semanticSearch) : ICorpusSearcher
{
    public string Name => name;

    public string? Description => description;

    public string Mode => "semantic";

    public async Task<IReadOnlyList<SearchSnippet>> SearchAsync(
        string query, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<SemanticSearchResult> results = await semanticSearch
            .SearchAsync(collectionName, query, limit, cancellationToken)
            .ConfigureAwait(false);

        return [.. results.Select(r => new SearchSnippet(r.Key, r.Text, r.Score, "semantic"))];
    }
}
