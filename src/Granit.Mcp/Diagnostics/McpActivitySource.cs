using System.Diagnostics;

namespace Granit.Mcp.Diagnostics;

/// <summary>
/// OpenTelemetry activity source for the Granit MCP module.
/// </summary>
internal static class McpActivitySource
{
    public const string Name = "Granit.Mcp";

    internal static readonly ActivitySource Instance = new(Name);
}
