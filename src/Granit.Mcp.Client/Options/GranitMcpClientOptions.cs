namespace Granit.Mcp.Client.Options;

/// <summary>
/// Configuration options for MCP client connections.
/// Bind from <c>appsettings.json</c> section <c>Mcp:Client</c>.
/// </summary>
public sealed class GranitMcpClientOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Mcp:Client";

    /// <summary>Named MCP server connections.</summary>
    public Dictionary<string, McpConnectionOptions> Connections { get; set; } = [];
}
