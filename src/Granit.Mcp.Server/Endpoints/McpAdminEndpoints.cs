using Granit.Mcp.Server.Internal;
using Granit.Mcp.Server.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Mcp.Server.Endpoints;

/// <summary>
/// Admin endpoints for inspecting registered MCP tools and OAuth scope mappings.
/// </summary>
internal static class McpAdminEndpoints
{
    /// <summary>
    /// Maps admin GET endpoints onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/tools", ListTools)
            .WithName("ListMcpTools")
            .WithSummary("Returns all registered MCP tools with their CLR type information.")
            .WithDescription(
                "Lists every MCP tool discovered during module assembly scanning. " +
                "Each entry includes the tool name and declaring CLR type (if resolved). " +
                "Useful for debugging tool visibility and verifying auto-discovery results.")
            .Produces<IReadOnlyList<McpToolInfoResponse>>();

        group.MapGet("/scopes", ListScopes)
            .WithName("ListMcpScopes")
            .WithSummary("Returns the mapping between OAuth scopes and MCP permissions.")
            .WithDescription(
                "Lists all known OAuth scope strings and their corresponding Granit MCP " +
                "permission constants. Useful for configuring OAuth client scopes in identity providers.")
            .Produces<IReadOnlyList<McpScopeMappingResponse>>();

        return group;
    }

    private static Ok<IReadOnlyList<McpToolInfoResponse>> ListTools(
        [FromServices] McpToolTypeRegistry registry)
    {
        IReadOnlyList<McpToolInfoResponse> tools = registry.GetAllRegistrations()
            .Select(kv => new McpToolInfoResponse(kv.Key, kv.Value.FullName ?? kv.Value.Name))
            .OrderBy(t => t.ToolName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return TypedResults.Ok(tools);
    }

    private static Ok<IReadOnlyList<McpScopeMappingResponse>> ListScopes()
    {
        IReadOnlyList<McpScopeMappingResponse> mappings = PermissionScopeMappingService.GetAllScopes()
            .Select(scope => new McpScopeMappingResponse(
                scope,
                PermissionScopeMappingService.MapScopeToPermission(scope)!))
            .OrderBy(m => m.Scope, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return TypedResults.Ok(mappings);
    }
}

/// <summary>MCP tool information for admin endpoints.</summary>
/// <param name="ToolName">The registered MCP tool name.</param>
/// <param name="ClrType">The fully qualified CLR type name.</param>
public sealed record McpToolInfoResponse(string ToolName, string ClrType);

/// <summary>OAuth scope to MCP permission mapping.</summary>
/// <param name="Scope">The OAuth scope string (e.g., <c>mcp:tools:read</c>).</param>
/// <param name="Permission">The Granit permission constant (e.g., <c>Mcp.Tools.Read</c>).</param>
public sealed record McpScopeMappingResponse(string Scope, string Permission);
