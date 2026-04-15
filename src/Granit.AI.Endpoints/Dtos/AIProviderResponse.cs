namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Represents a registered AI provider in the discovery response.
/// </summary>
/// <param name="Name">The provider identifier (e.g. <c>"OpenAI"</c>, <c>"Ollama"</c>).</param>
/// <param name="SupportsChat">Whether this provider supports chat completions.</param>
/// <param name="SupportsEmbeddings">Whether this provider supports embedding generation.</param>
public sealed record AIProviderResponse(
    string Name,
    bool SupportsChat,
    bool SupportsEmbeddings);
