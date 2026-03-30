using Granit.Mcp.Server.Endpoints;
using Granit.Mcp.Server.Options;
using Granit.Mcp.Server.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    /// <param name="configure">Optional configuration callback (overrides appsettings values).</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitMcpServer(
        this IEndpointRouteBuilder endpoints,
        Action<GranitMcpServerOptions>? configure = null)
    {
        // Resolve options from DI (bound via BindConfiguration in AddGranitMcpServer)
        GranitMcpServerOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<GranitMcpServerOptions>>()
            .Value;

        // Apply optional callback overrides
        configure?.Invoke(options);

        // SDK: MapMcp handles both Streamable HTTP and legacy SSE
        IEndpointConventionBuilder mcpEndpoint = endpoints.MapMcp(options.RoutePrefix);

        if (options.RequireAuthentication)
        {
            mcpEndpoint.RequireAuthorization(McpPermissions.Server.Access);
        }

        // Admin endpoints: tool listing and scope mapping
        if (options.MapAdminEndpoints)
        {
            RouteGroupBuilder adminGroup = endpoints
                .MapGroup($"{options.RoutePrefix}/admin")
                .WithTags(options.AdminTagName)
                .RequireAuthorization(McpPermissions.Tools.Read);

            adminGroup.MapAdminEndpoints();
        }

        return endpoints;
    }
}
