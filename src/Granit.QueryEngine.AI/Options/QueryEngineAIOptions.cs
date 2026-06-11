namespace Granit.QueryEngine.AI.Options;

/// <summary>
/// Configuration options for the AI-powered natural language query translator.
/// </summary>
public sealed class QueryEngineAIOptions
{
    /// <summary>
    /// Configuration section name (<c>"QueryEngine:AI"</c>).
    /// </summary>
    public const string SectionName = "QueryEngine:AI";

    /// <summary>
    /// AI workspace name used to create the <c>IChatClient</c>. Defaults to <c>"default"</c>.
    /// </summary>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Timeout in seconds for the LLM call. NLQ should be fast. Defaults to <c>5</c>.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}
