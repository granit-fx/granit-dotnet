namespace Granit.Mcp.Options;

/// <summary>
/// Controls how MCP tools are discovered from module assemblies.
/// </summary>
public enum McpToolDiscoveryMode
{
    /// <summary>
    /// Only classes annotated with both <c>[McpServerToolType]</c> (SDK) and
    /// <c>[McpExposed]</c> (Granit) are discovered. Recommended for production
    /// to prevent accidental exposure of internal services.
    /// </summary>
    Explicit,

    /// <summary>
    /// All <c>[McpServerToolType]</c> classes from module assemblies are discovered
    /// automatically. Suitable for development and staging environments.
    /// </summary>
    Auto,
}
