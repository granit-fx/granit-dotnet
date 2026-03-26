using System.Diagnostics.CodeAnalysis;
using Granit.AI.Mcp.Internal;
using Granit.AI.Mcp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Mcp.Extensions;

/// <summary>
/// Extension methods for registering the Granit AI-MCP bridge.
/// Called by <see cref="GranitAIMcpModule.ConfigureServices"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AIMcpServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit AI-MCP bridge services: MCP tool source provider for AI workspaces
    /// and sampling guard for cost-controlled LLM completions.
    /// </summary>
    internal static IServiceCollection AddGranitAIMcp(this IServiceCollection services)
    {
        services
            .AddOptions<GranitAIMcpOptions>()
            .BindConfiguration(GranitAIMcpOptions.SectionName);

        services.TryAddSingleton<IMcpToolSourceProvider, McpToolSourceProvider>();
        services.TryAddSingleton<SamplingGuard>();

        return services;
    }
}
