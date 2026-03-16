namespace Granit.AI.VectorData;

/// <summary>
/// High-level semantic search service combining embedding generation and vector search.
/// </summary>
/// <remarks>
/// Uses <see cref="IVectorCollectionFactory"/> for vector storage and
/// <see cref="Granit.AI.IEmbeddingGeneratorFactory"/> for generating embeddings from text.
/// </remarks>
public interface ISemanticSearchService
{
    /// <summary>
    /// Indexes a text document by generating its embedding and storing it in a vector collection.
    /// </summary>
    /// <param name="collectionName">The logical name of the vector collection.</param>
    /// <param name="key">The unique key for the document.</param>
    /// <param name="text">The text content to embed and index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexAsync(string collectionName, string key, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for documents similar to the given query text.
    /// </summary>
    /// <param name="collectionName">The logical name of the vector collection.</param>
    /// <param name="query">The natural language query to search for.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of semantic search results ordered by descending relevance.</returns>
    Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        string collectionName,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
