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

    /// <summary>Whether to map admin endpoints (tool listing). Default: <see langword="true"/>.</summary>
    public bool MapAdminEndpoints { get; set; } = true;

    /// <summary>OpenAPI tag name for admin endpoints.</summary>
    public string AdminTagName { get; set; } = "MCP Admin";

    /// <summary>
    /// Only expose tools from these module namespaces.
    /// Empty set means all discovered modules are exposed.
    /// Example: <c>["BlobStorage", "Workflow"]</c>.
    /// </summary>
    public HashSet<string> EnabledModules { get; set; } = [];
}
