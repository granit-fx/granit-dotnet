namespace Granit.AI.VectorData;

/// <summary>
/// A result from a semantic search operation.
/// </summary>
/// <param name="Key">The unique key of the matched document.</param>
/// <param name="Score">The similarity score (higher is more similar).</param>
/// <param name="Text">The text content of the matched document, if available.</param>
public sealed record SemanticSearchResult(string Key, double Score, string? Text);
