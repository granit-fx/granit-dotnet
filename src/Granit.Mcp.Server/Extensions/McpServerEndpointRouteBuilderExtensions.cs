using Granit.Mcp.Server.Options;
using Granit.Mcp.Server.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModelContextProtocol.AspNetCore;

namespace Granit.Mcp.Server.Extensions;

/// <summary>
/// Extension methods for mapping the MCP server endpoints.
/// </summary>
public static class McpServerEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the MCP Streamable HTTP endpoint and optional admin endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitMcpServer(
        this IEndpointRouteBuilder endpoints,
        Action<GranitMcpServerOptions>? configure = null)
    {
        GranitMcpServerOptions options = new();
        configure?.Invoke(options);

        // SDK: MapMcp handles both Streamable HTTP and legacy SSE
        IEndpointConventionBuilder mcpEndpoint = endpoints.MapMcp(options.RoutePrefix);

        if (options.RequireAuthentication)
        {
            mcpEndpoint.RequireAuthorization(McpPermissions.Server.Access);
        }

        return endpoints;
    }
}
