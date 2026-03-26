namespace Granit.Mcp.Client.Options;

/// <summary>
/// Configuration for a single MCP server connection.
/// </summary>
public sealed class McpConnectionOptions
{
    /// <summary>Server URL for HTTP transport. Required when <see cref="Transport"/> is <c>"http"</c>.</summary>
    public string? Url { get; set; }

    /// <summary>Transport type: <c>"http"</c> (default) or <c>"stdio"</c>.</summary>
    public string Transport { get; set; } = "http";

    /// <summary>
    /// Allow insecure HTTP (non-TLS) transport. Default: <see langword="false"/>.
    /// Enable only for local development (e.g., Ollama on localhost).
    /// </summary>
    public bool AllowInsecureTransport { get; set; }

    /// <summary>Command for stdio transport. Required when <see cref="Transport"/> is <c>"stdio"</c>.</summary>
    public string? Command { get; set; }

    /// <summary>Arguments for stdio transport command.</summary>
    public string[] Arguments { get; set; } = [];
}
