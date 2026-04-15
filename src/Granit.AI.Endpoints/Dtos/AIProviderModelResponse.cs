namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Represents an AI model available from a provider.
/// </summary>
/// <param name="Id">The model identifier used in API calls.</param>
/// <param name="DisplayName">A human-readable name for display in the UI.</param>
/// <param name="Capabilities">The capabilities this model supports.</param>
/// <param name="MaxContextTokens">Optional maximum context window size in tokens.</param>
public sealed record AIProviderModelResponse(
    string Id,
    string DisplayName,
    AIModelCapabilities Capabilities,
    int? MaxContextTokens);
