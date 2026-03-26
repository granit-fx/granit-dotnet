namespace Granit.Mcp.Options;

/// <summary>
/// Configuration options for the Granit MCP module.
/// Bind from <c>appsettings.json</c> section <c>Mcp</c>.
/// </summary>
public sealed class GranitMcpOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Mcp";

    /// <summary>Server name exposed in MCP server info. Default: <c>"Granit"</c>.</summary>
    public string ServerName { get; set; } = "Granit";

    /// <summary>Server version exposed in MCP server info.</summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// Tool discovery mode.
    /// <list type="bullet">
    /// <item><see cref="McpToolDiscoveryMode.Explicit"/> (default): only classes with
    /// both <c>[McpServerToolType]</c> and <c>[McpExposed]</c> are discovered.</item>
    /// <item><see cref="McpToolDiscoveryMode.Auto"/>: all <c>[McpServerToolType]</c>
    /// classes from module assemblies are discovered.</item>
    /// </list>
    /// </summary>
    public McpToolDiscoveryMode ToolDiscovery { get; set; } = McpToolDiscoveryMode.Explicit;

    /// <summary>Whether to enable multi-tenant tool filtering via <c>[McpTenantScope]</c>.</summary>
    public bool EnableTenantFiltering { get; set; } = true;

    /// <summary>Maximum response size in bytes before truncation. Default: 51200 (50 KB).</summary>
    public int MaxResponseSizeBytes { get; set; } = 51_200;
}
