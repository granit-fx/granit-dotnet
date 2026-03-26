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

    /// <summary>Whether to forward the current user's JWT to the remote MCP server. Default: <see langword="true"/>.</summary>
    public bool ForwardCredentials { get; set; } = true;

    /// <summary>Command for stdio transport. Required when <see cref="Transport"/> is <c>"stdio"</c>.</summary>
    public string? Command { get; set; }

    /// <summary>Arguments for stdio transport command.</summary>
    public string[] Arguments { get; set; } = [];
}
