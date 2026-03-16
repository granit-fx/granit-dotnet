namespace Granit.AI.VectorData.Internal;

/// <summary>
/// Internal record used by <see cref="DefaultSemanticSearchService"/> for text-based vector storage.
/// </summary>
internal sealed class TextVectorRecord
{
    /// <summary>
    /// The unique key of the document.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The original text content of the document.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// The embedding vector for the document.
    /// </summary>
    public required ReadOnlyMemory<float> Vector { get; init; }
}
