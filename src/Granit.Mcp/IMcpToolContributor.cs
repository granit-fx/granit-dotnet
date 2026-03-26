using Microsoft.Extensions.DependencyInjection;

namespace Granit.Mcp;

/// <summary>
/// Imperative escape hatch for advanced MCP tool registration.
/// Use when declarative discovery (<c>[McpServerToolType]</c> + <c>[McpToolOptions]</c>)
/// is insufficient — e.g., dynamic tools, complex icon sets with multiple sizes,
/// or runtime-generated tool definitions.
/// </summary>
/// <remarks>
/// Implementations are auto-discovered from module assemblies (same pattern as
/// <c>IPermissionDefinitionProvider</c>). They must have a parameterless constructor.
/// </remarks>
public interface IMcpToolContributor
{
    /// <summary>
    /// Configures additional tools, resources, or prompts on the MCP server builder.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    void Configure(IMcpServerBuilder builder);
}
