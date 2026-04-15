namespace Granit.AI;

/// <summary>
/// Describes the capabilities supported by an AI model.
/// </summary>
/// <param name="Chat">Whether the model supports chat completions. Default <c>true</c>.</param>
/// <param name="Embeddings">Whether the model supports embedding generation. Default <c>false</c>.</param>
public sealed record AIModelCapabilities(
    bool Chat = true,
    bool Embeddings = false);
