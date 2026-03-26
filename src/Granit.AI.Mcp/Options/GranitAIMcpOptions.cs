namespace Granit.AI.Mcp.Options;

/// <summary>
/// Configuration options for the AI-MCP bridge.
/// Bind from <c>appsettings.json</c> section <c>AI:Mcp</c>.
/// </summary>
public sealed class GranitAIMcpOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AI:Mcp";

    /// <summary>
    /// Enable MCP sampling (external servers requesting LLM completions).
    /// Default: <see langword="false"/> — opt-in to prevent accidental cost exposure.
    /// </summary>
    public bool EnableSampling { get; set; }

    /// <summary>Workspace to use for sampling requests.</summary>
    public string SamplingWorkspace { get; set; } = "default";

    /// <summary>Max sampling requests per tenant per minute. 0 = unlimited.</summary>
    public int SamplingRateLimitPerMinute { get; set; } = 10;

    /// <summary>
    /// Max tokens per sampling request. Rejects <c>CreateMessage</c> if
    /// <c>maxTokens</c> exceeds this. 0 = unlimited.
    /// </summary>
    public int SamplingMaxTokensPerRequest { get; set; } = 2000;

    /// <summary>MCP client connection names to use as tool sources for AI workspaces.</summary>
    public List<string> ToolSourceConnections { get; set; } = [];
}
