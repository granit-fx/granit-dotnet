namespace Granit.AI.VectorData;

/// <summary>
/// A tenant-scoped collection of vector records for semantic search.
/// </summary>
/// <typeparam name="TRecord">The record type stored in this collection.</typeparam>
public interface IVectorCollection<TRecord> where TRecord : class
{
    /// <summary>
    /// Upserts a record with its embedding vector.
    /// </summary>
    /// <param name="record">The record to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a record by key.
    /// </summary>
    /// <param name="key">The unique key of the record to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for records similar to the given vector.
    /// </summary>
    /// <param name="vector">Query embedding vector.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of search results ordered by descending similarity score.</returns>
    Task<IReadOnlyList<VectorSearchResult<TRecord>>> SearchAsync(
        ReadOnlyMemory<float> vector,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
