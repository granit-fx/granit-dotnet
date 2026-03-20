namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Request to generate embeddings for text inputs.
/// </summary>
/// <param name="Inputs">List of text inputs to embed.</param>
public sealed record AIEmbeddingRequest(
    IReadOnlyList<string> Inputs);
