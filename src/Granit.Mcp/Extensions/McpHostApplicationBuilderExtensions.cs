using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Granit.Diagnostics;
using Granit.Mcp.Diagnostics;
using Granit.Mcp.Options;
using Granit.Mcp.Sanitization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Granit.Mcp.Extensions;

/// <summary>
/// Extension methods for registering Granit MCP core services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit MCP core services: MCP server, output sanitization pipeline,
    /// and OpenTelemetry diagnostics. Called by <see cref="GranitMcpModule"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The MCP server builder for further configuration.</returns>
    internal static IMcpServerBuilder AddGranitMcp(this IServiceCollection services)
    {
        GranitActivitySourceRegistry.Register(McpActivitySource.Name);

        services
            .AddOptions<GranitMcpOptions>()
            .BindConfiguration(GranitMcpOptions.SectionName);

        services.TryAddSingleton<McpMetrics>();

        // SDK: register MCP server
        IMcpServerBuilder mcpBuilder = services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation { Name = "Granit", Version = "1.0.0" };
        });

        // Default sanitizer: response size limit
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMcpOutputSanitizer, ResponseSizeLimitSanitizer>());

        return mcpBuilder;
    }

    /// <summary>
    /// Registers the SDK filter pipeline with Granit cross-cutting concerns:
    /// tool visibility, output sanitization, and audit/metrics.
    /// </summary>
    internal static IMcpServerBuilder WithGranitFilters(this IMcpServerBuilder mcpBuilder)
    {
        mcpBuilder.WithRequestFilters(filters =>
        {
            // Filter 1: Tool visibility (tenant, module scope, [McpExposed])
            filters.AddListToolsFilter(next => async (context, ct) =>
            {
                ListToolsResult result = await next(context, ct);

                IEnumerable<IMcpToolVisibilityFilter> visibilityFilters =
                    context.Services!.GetServices<IMcpToolVisibilityFilter>();

                if (!visibilityFilters.Any())
                {
                    return result;
                }

                var visibleTools = new List<Tool>();
                foreach (Tool tool in result.Tools)
                {
                    bool isVisible = true;
                    foreach (IMcpToolVisibilityFilter filter in visibilityFilters)
                    {
                        if (!await filter.IsVisibleAsync(tool.Name, toolType: null, context.Services!, ct))
                        {
                            isVisible = false;
                            break;
                        }
                    }

                    if (isVisible)
                    {
                        visibleTools.Add(tool);
                    }
                }

                result.Tools = visibleTools;
                return result;
            });

            // Filter 2: Output sanitization (GDPR) + audit/metrics
            filters.AddCallToolFilter(next => async (context, ct) =>
            {
                McpMetrics? metrics = context.Services?.GetService<McpMetrics>();
                string toolName = context.Params?.Name ?? "unknown";
                var sw = Stopwatch.StartNew();

                try
                {
                    CallToolResult result = await next(context, ct);
                    sw.Stop();

                    // Sanitize output
                    IEnumerable<IMcpOutputSanitizer> sanitizers =
                        context.Services!.GetServices<IMcpOutputSanitizer>();

                    foreach (IMcpOutputSanitizer sanitizer in sanitizers)
                    {
                        result = await sanitizer.SanitizeAsync(result, context.Services!, ct);
                    }

                    metrics?.RecordToolInvoked(tenantId: null, toolName, "success");
                    metrics?.RecordRequestDuration(tenantId: null, $"tools/call/{toolName}", sw.Elapsed);

                    return result;
                }
                catch (Exception)
                {
                    sw.Stop();
                    metrics?.RecordToolInvoked(tenantId: null, toolName, "error");
                    metrics?.RecordRequestDuration(tenantId: null, $"tools/call/{toolName}", sw.Elapsed);
                    throw;
                }
            });
        });

        return mcpBuilder;
    }

    /// <summary>
    /// Scans module assemblies for MCP tools, prompts, and <see cref="IMcpToolContributor"/>
    /// implementations, then registers them with the MCP server.
    /// </summary>
    internal static IMcpServerBuilder DiscoverFromAssemblies(
        this IMcpServerBuilder mcpBuilder,
        IReadOnlyCollection<Assembly> moduleAssemblies)
    {
        // Path 1: Assembly scanning — tools, prompts
        foreach (Assembly assembly in moduleAssemblies)
        {
            mcpBuilder.WithToolsFromAssembly(assembly);
            mcpBuilder.WithPromptsFromAssembly(assembly);
        }

        // Path 2: IMcpToolContributor — imperative registrations
        foreach (Assembly assembly in moduleAssemblies)
        {
            IEnumerable<Type> contributorTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                    && typeof(IMcpToolContributor).IsAssignableFrom(t));

            foreach (Type contributorType in contributorTypes)
            {
                var contributor = (IMcpToolContributor)Activator.CreateInstance(contributorType)!;
                contributor.Configure(mcpBuilder);
            }
        }

        return mcpBuilder;
    }
}
