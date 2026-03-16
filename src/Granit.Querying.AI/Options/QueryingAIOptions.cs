namespace Granit.Querying.AI.Options;

/// <summary>
/// Configuration options for the AI-powered natural language query translator.
/// </summary>
public sealed class QueryingAIOptions
{
    /// <summary>
    /// Configuration section name (<c>"AI:Querying"</c>).
    /// </summary>
    public const string SectionName = "AI:Querying";

    /// <summary>
    /// AI workspace name used to create the <c>IChatClient</c>. Defaults to <c>"default"</c>.
    /// </summary>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Timeout in seconds for the LLM call. NLQ should be fast. Defaults to <c>5</c>.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}
