namespace Granit.Mcp.Server.Permissions;

/// <summary>
/// Permission constants for MCP server access and operations.
/// </summary>
public static class McpPermissions
{
    public const string GroupName = "Mcp";

    public static class Server
    {
        /// <summary>Grants access to the MCP server endpoint.</summary>
        public const string Access = "Mcp.Server.Access";
    }

    public static class Tools
    {
        /// <summary>Grants read access to the tool registry (diagnostics endpoints).</summary>
        public const string Read = "Mcp.Tools.Read";

        /// <summary>Grants execution access to MCP tools.</summary>
        public const string Execute = "Mcp.Tools.Execute";
    }

    public static class Resources
    {
        /// <summary>Grants read access to MCP resources.</summary>
        public const string Read = "Mcp.Resources.Read";
    }

    public static class Prompts
    {
        /// <summary>Grants read access to MCP prompts.</summary>
        public const string Read = "Mcp.Prompts.Read";
    }
}
