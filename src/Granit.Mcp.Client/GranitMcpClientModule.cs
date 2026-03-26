using Granit.Modularity;

namespace Granit.Mcp.Client;

/// <summary>
/// Granit module for MCP client connections.
/// Provides <see cref="IMcpClientFactory"/> for connecting to external MCP servers.
/// </summary>
[DependsOn(typeof(GranitMcpModule))]
public sealed class GranitMcpClientModule : GranitModule;
