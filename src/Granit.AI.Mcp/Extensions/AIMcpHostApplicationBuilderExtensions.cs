using System.Diagnostics.CodeAnalysis;
using Granit.AI.Mcp.Internal;
using Granit.AI.Mcp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.AI.Mcp.Extensions;

/// <summary>
/// Extension methods for registering the Granit AI-MCP bridge.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AIMcpHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Granit AI-MCP bridge services: MCP tool source provider for AI workspaces
    /// and sampling guard for cost-controlled LLM completions.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAIMcp(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<GranitAIMcpOptions>()
            .BindConfiguration(GranitAIMcpOptions.SectionName);

        builder.Services.TryAddSingleton<IMcpToolSourceProvider, McpToolSourceProvider>();
        builder.Services.TryAddSingleton<SamplingGuard>();

        return builder;
    }
}
