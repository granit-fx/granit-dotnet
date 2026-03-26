using Granit.AI.Mcp.Options;
using Granit.Mcp.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace Granit.AI.Mcp.Internal;

/// <summary>
/// Resolves MCP tools from connected servers and wraps them as <see cref="AITool"/>.
/// </summary>
internal sealed class McpToolSourceProvider(
    IMcpClientFactory clientFactory,
    IOptions<GranitAIMcpOptions> options) : IMcpToolSourceProvider
{
    public async Task<IReadOnlyList<AITool>> GetToolsAsync(
        string workspaceName,
        CancellationToken cancellationToken = default)
    {
        List<string> connections = options.Value.ToolSourceConnections;
        if (connections.Count == 0)
        {
            return [];
        }

        List<AITool> tools = [];
        foreach (string connectionName in connections)
        {
            await using McpClient client = await clientFactory.CreateAsync(connectionName, cancellationToken);
            IList<McpClientTool> mcpTools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            tools.AddRange(mcpTools);
        }

        return tools;
    }
}
