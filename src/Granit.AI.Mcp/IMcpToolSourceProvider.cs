using Microsoft.Extensions.AI;

namespace Granit.AI.Mcp;

/// <summary>
/// Provides MCP tools from external servers as <see cref="AITool"/> instances
/// for injection into AI workspace <c>IChatClient</c> pipelines.
/// </summary>
public interface IMcpToolSourceProvider
{
    /// <summary>
    /// Returns AI function tools from connected MCP servers for a given workspace.
    /// </summary>
    /// <param name="workspaceName">The AI workspace name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of AI tools backed by MCP server calls.</returns>
    Task<IReadOnlyList<AITool>> GetToolsAsync(
        string workspaceName,
        CancellationToken cancellationToken = default);
}
