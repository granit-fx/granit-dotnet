using ModelContextProtocol.Client;

namespace Granit.Mcp.Client;

/// <summary>
/// Factory for creating MCP client connections to external servers.
/// Supports named connections configured in <c>Mcp:Client:Connections</c>.
/// </summary>
/// <remarks>
/// Follows the same named-factory pattern as <c>IHttpClientFactory</c>.
/// HTTP connections support automatic credential forwarding from the current user's JWT.
/// </remarks>
public interface IMcpClientFactory
{
    /// <summary>
    /// Creates an MCP client for a named connection.
    /// </summary>
    /// <param name="connectionName">The connection name from configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected MCP client. Caller owns disposal.</returns>
    Task<McpClient> CreateAsync(string connectionName, CancellationToken cancellationToken = default);
}
