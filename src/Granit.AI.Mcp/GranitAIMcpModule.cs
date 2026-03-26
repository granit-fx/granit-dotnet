using Granit.AI;
using Granit.AI.Mcp.Extensions;
using Granit.Mcp.Client;
using Granit.Modularity;

namespace Granit.AI.Mcp;

/// <summary>
/// Granit module bridging MCP tools into AI workspaces.
/// Provides <see cref="IMcpToolSourceProvider"/> for injecting external MCP tools
/// into <c>IChatClient</c> pipelines and a sampling guard for cost-controlled LLM access.
/// </summary>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitMcpClientModule))]
public sealed class GranitAIMcpModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitAIMcp();
}
