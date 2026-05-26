namespace Granit.Indexing;

/// <summary>
/// A single backend match: identifier, projected result row, and relevance score.
/// </summary>
/// <typeparam name="TKey">Resource primary key.</typeparam>
/// <typeparam name="TResult">Consumer-facing projection (response DTO, summary record, …).</typeparam>
/// <param name="Key">Resource key; used by the over-fetch loop to ask the authorizer.</param>
/// <param name="Result">Projected payload returned to the caller after filtering.</param>
/// <param name="Score">
/// Backend-specific relevance score. Higher = more relevant. The shape (range, units)
/// depends on the backend (tsvector ranking, BM25, cosine similarity…); compare
/// scores only within a single response.
/// </param>
public sealed record SearchHit<TKey, TResult>(TKey Key, TResult Result, double Score);
