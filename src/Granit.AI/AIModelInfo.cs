namespace Granit.AI;

/// <summary>
/// Describes an AI model available from a provider.
/// </summary>
/// <param name="Id">The model identifier used in API calls (e.g. <c>"gpt-4o"</c>, <c>"llama3.1"</c>).</param>
/// <param name="DisplayName">A human-readable name for the model.</param>
/// <param name="Capabilities">The capabilities this model supports.</param>
/// <param name="MaxContextTokens">Optional maximum context window size in tokens.</param>
public sealed record AIModelInfo(
    string Id,
    string DisplayName,
    AIModelCapabilities Capabilities,
    int? MaxContextTokens = null);
