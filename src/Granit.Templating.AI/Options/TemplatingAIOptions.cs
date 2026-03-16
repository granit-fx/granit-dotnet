namespace Granit.Templating.AI.Options;

/// <summary>
/// Configuration options for AI-powered template assistance.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Templating</c> configuration section.
/// </remarks>
public sealed class TemplatingAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "AI:Templating";

    /// <summary>
    /// Name of the AI workspace to use for template generation.
    /// </summary>
    /// <remarks>
    /// Must correspond to a workspace configured in the <c>AI:Workspaces</c> section.
    /// Defaults to <c>"default"</c>.
    /// </remarks>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Timeout in seconds for the LLM call.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
