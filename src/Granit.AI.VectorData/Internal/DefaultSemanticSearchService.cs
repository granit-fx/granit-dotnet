using Granit.AI.VectorData.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AI.VectorData.Internal;

/// <summary>
/// Default implementation of <see cref="ISemanticSearchService"/> that combines
/// embedding generation via <see cref="IAIEmbeddingGeneratorFactory"/> with
/// vector storage via <see cref="IVectorCollectionFactory"/>.
/// </summary>
internal sealed partial class DefaultSemanticSearchService(
    IAIEmbeddingGeneratorFactory embeddingGeneratorFactory,
    IVectorCollectionFactory vectorCollectionFactory,
    IOptions<VectorDataOptions> options,
    ILogger<DefaultSemanticSearchService> logger) : ISemanticSearchService
{
    /// <inheritdoc />
    public async Task IndexAsync(
        string collectionName,
        string key,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionName);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(text);

        LogIndexing(collectionName, key);

        IEmbeddingGenerator<string, Embedding<float>> generator =
            await embeddingGeneratorFactory.CreateAsync(options.Value.EmbeddingWorkspace, cancellationToken).ConfigureAwait(false);

        GeneratedEmbeddings<Embedding<float>> embeddings =
            await generator.GenerateAsync([text], cancellationToken: cancellationToken).ConfigureAwait(false);

        var record = new TextVectorRecord
        {
            Key = key,
            Text = text,
            Vector = embeddings[0].Vector,
        };

        IVectorCollection<TextVectorRecord> collection =
            vectorCollectionFactory.GetCollection<TextVectorRecord>(collectionName);

        await collection.UpsertAsync(record, cancellationToken).ConfigureAwait(false);

        LogIndexed(collectionName, key);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        string collectionName,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionName);
        ArgumentNullException.ThrowIfNull(query);

        int effectiveLimit = limit > 0 ? limit : options.Value.DefaultSearchLimit;

        LogSearching(collectionName, effectiveLimit);

        IEmbeddingGenerator<string, Embedding<float>> generator =
            await embeddingGeneratorFactory.CreateAsync(options.Value.EmbeddingWorkspace, cancellationToken).ConfigureAwait(false);

        GeneratedEmbeddings<Embedding<float>> embeddings =
            await generator.GenerateAsync([query], cancellationToken: cancellationToken).ConfigureAwait(false);

        IVectorCollection<TextVectorRecord> collection =
            vectorCollectionFactory.GetCollection<TextVectorRecord>(collectionName);

        IReadOnlyList<VectorSearchResult<TextVectorRecord>> results =
            await collection.SearchAsync(embeddings[0].Vector, effectiveLimit, cancellationToken).ConfigureAwait(false);

        LogSearchCompleted(collectionName, results.Count);

        return results
            .Select(r => new SemanticSearchResult(r.Record.Key, r.Score, r.Record.Text))
            .ToList();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Indexing document in collection '{CollectionName}' with key '{Key}'")]
    private partial void LogIndexing(string collectionName, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Indexed document in collection '{CollectionName}' with key '{Key}'")]
    private partial void LogIndexed(string collectionName, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Searching collection '{CollectionName}' with limit {Limit}")]
    private partial void LogSearching(string collectionName, int limit);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Search completed in collection '{CollectionName}', found {Count} results")]
    private partial void LogSearchCompleted(string collectionName, int count);
}
