namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Response containing generated embeddings.
/// </summary>
/// <param name="WorkspaceName">Workspace that processed the request.</param>
/// <param name="Model">Model used for embedding generation.</param>
/// <param name="Embeddings">Generated embedding vectors.</param>
public sealed record AIEmbeddingResponse(
    string WorkspaceName,
    string Model,
    IReadOnlyList<AIEmbeddingDataResponse> Embeddings);

/// <summary>
/// A single embedding vector with its index.
/// </summary>
/// <param name="Index">Position in the input list.</param>
/// <param name="Vector">Embedding vector values.</param>
public sealed record AIEmbeddingDataResponse(
    int Index,
    IReadOnlyList<float> Vector);
