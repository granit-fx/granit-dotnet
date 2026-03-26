using Granit.Mcp.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Mcp;

/// <summary>
/// Granit module for MCP (Model Context Protocol) integration.
/// Auto-discovers <c>[McpServerToolType]</c> and <c>[McpServerPromptType]</c>
/// classes from loaded module assemblies. Registers output sanitization, tool
/// visibility filters, and OpenTelemetry diagnostics into the SDK's filter pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Authorization uses standard <c>[Authorize(Policy = "...")]</c> via the SDK's
/// <c>.AddAuthorizationFilters()</c>. Granit's <c>DynamicPermissionPolicyProvider</c>
/// maps policy names to Granit permissions automatically.
/// </para>
/// <para>
/// For HTTP transport, add <c>Granit.Mcp.Server</c> which provides
/// <c>AddGranitMcpServer()</c> and <c>MapGranitMcpServer()</c>.
/// </para>
/// </remarks>
public sealed class GranitMcpModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services
            .AddGranitMcp()
            .DiscoverFromAssemblies(context.ModuleAssemblies)
            .WithGranitFilters();
    }
}
