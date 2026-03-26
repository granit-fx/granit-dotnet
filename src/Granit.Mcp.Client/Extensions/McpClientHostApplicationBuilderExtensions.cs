using System.Diagnostics.CodeAnalysis;
using Granit.Mcp.Client.Internal;
using Granit.Mcp.Client.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Mcp.Client.Extensions;

/// <summary>
/// Extension methods for registering Granit MCP client services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class McpClientHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Granit MCP client services: named client factory for connecting to
    /// external MCP servers via HTTP or stdio transport.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitMcpClient(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<GranitMcpClientOptions>()
            .BindConfiguration(GranitMcpClientOptions.SectionName);

        builder.Services.TryAddSingleton<IMcpClientFactory, DefaultMcpClientFactory>();

        return builder;
    }
}
