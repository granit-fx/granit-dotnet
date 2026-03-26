using System.Diagnostics.CodeAnalysis;
using Granit.Mcp.Client.Internal;
using Granit.Mcp.Client.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Mcp.Client.Extensions;

/// <summary>
/// Extension methods for registering Granit MCP client services.
/// Called by <see cref="GranitMcpClientModule.ConfigureServices"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public static class McpClientServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit MCP client services: named client factory for connecting to
    /// external MCP servers via HTTP or stdio transport.
    /// </summary>
    internal static IServiceCollection AddGranitMcpClient(this IServiceCollection services)
    {
        services
            .AddOptions<GranitMcpClientOptions>()
            .BindConfiguration(GranitMcpClientOptions.SectionName);

        services.TryAddSingleton<IMcpClientFactory, DefaultMcpClientFactory>();

        return services;
    }
}
