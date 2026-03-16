namespace Granit.AI.VectorData;

/// <summary>
/// A search result from vector similarity search.
/// </summary>
/// <typeparam name="TRecord">The record type stored in the vector collection.</typeparam>
/// <param name="Record">The matched record.</param>
/// <param name="Score">The similarity score (higher is more similar).</param>
public sealed record VectorSearchResult<TRecord>(TRecord Record, double Score) where TRecord : class;
