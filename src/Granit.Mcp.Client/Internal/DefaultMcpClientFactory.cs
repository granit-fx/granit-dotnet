using Granit.Mcp.Client.Options;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Granit.Mcp.Client.Internal;

/// <summary>
/// Default implementation of <see cref="IMcpClientFactory"/>.
/// Creates MCP clients from named connection configurations.
/// </summary>
internal sealed class DefaultMcpClientFactory(IOptions<GranitMcpClientOptions> options) : IMcpClientFactory
{
    public async Task<McpClient> CreateAsync(string connectionName, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Connections.TryGetValue(connectionName, out McpConnectionOptions? connection))
        {
            throw new InvalidOperationException(
                $"MCP connection '{connectionName}' is not configured. " +
                $"Add it to the '{GranitMcpClientOptions.SectionName}:Connections' section in appsettings.json.");
        }

        IClientTransport transport = CreateTransport(connectionName, connection);
        return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
    }

    private static IClientTransport CreateTransport(string name, McpConnectionOptions connection)
    {
        return connection.Transport.ToLowerInvariant() switch
        {
            "http" => CreateHttpTransport(name, connection),
            "stdio" => CreateStdioTransport(name, connection),
            _ => throw new InvalidOperationException(
                $"Unsupported MCP transport type '{connection.Transport}' for connection '{name}'. " +
                "Supported values: 'http', 'stdio'."),
        };
    }

    private static HttpClientTransport CreateHttpTransport(string name, McpConnectionOptions connection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Url);

        return new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(connection.Url),
            Name = name,
        });
    }

    private static StdioClientTransport CreateStdioTransport(string name, McpConnectionOptions connection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Command);

        return new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = name,
            Command = connection.Command,
            Arguments = connection.Arguments,
        });
    }
}
