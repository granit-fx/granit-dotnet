using Granit.Authorization;
using Granit.Mcp.Server.Extensions;
using Granit.Modularity;

namespace Granit.Mcp.Server;

/// <summary>
/// Granit module for the MCP server (ASP.NET Core HTTP transport).
/// Adds Streamable HTTP transport, SDK authorization filters, tool visibility
/// filters (tenant, module scope, explicit discovery), and error sanitization.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitMcpModule))]
public sealed class GranitMcpServerModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitMcpServer();
}
