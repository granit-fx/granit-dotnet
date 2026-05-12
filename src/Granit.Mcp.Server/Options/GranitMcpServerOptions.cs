namespace Granit.Mcp.Server.Options;

/// <summary>
/// Configuration options for the Granit MCP server.
/// Bind from <c>appsettings.json</c> section <c>Mcp:Server</c>.
/// </summary>
public sealed class GranitMcpServerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Mcp:Server";

    /// <summary>Route prefix for the MCP endpoint. Default: <c>"/mcp"</c>.</summary>
    public string RoutePrefix { get; set; } = "/mcp";

    /// <summary>Whether to require authentication for MCP requests. Default: <see langword="true"/>.</summary>
    public bool RequireAuthentication { get; set; } = true;

    /// <summary>
    /// Whether to map diagnostics endpoints (tool registry inspection, OAuth scope mapping).
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool MapDiagnosticsEndpoints { get; set; } = true;

    /// <summary>
    /// Route prefix for diagnostics endpoints. Default: <c>"/mcp/diagnostics"</c>.
    /// Kept disjoint from <see cref="RoutePrefix"/> to avoid colliding with the MCP SDK's
    /// child-route handling on the MapMcp prefix.
    /// </summary>
    public string DiagnosticsRoutePrefix { get; set; } = "/mcp/diagnostics";

    /// <summary>OpenAPI tag name for diagnostics endpoints.</summary>
    public string DiagnosticsTagName { get; set; } = "MCP - Diagnostics";

    /// <summary>
    /// Only expose tools from these module namespaces.
    /// Empty set means all discovered modules are exposed.
    /// Example: <c>["BlobStorage", "Workflow"]</c>.
    /// </summary>
    public HashSet<string> EnabledModules { get; set; } = [];
}
