using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Granit.DataProtection;
using Granit.Diagnostics;
using Granit.Mcp.Diagnostics;
using Granit.Mcp.Options;
using Granit.Mcp.Sanitization;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
    internal static IMcpServerBuilder AddGranitMcp(this IServiceCollection services)
    {
        GranitActivitySourceRegistry.Register(McpActivitySource.Name);

        services
            .AddOptions<GranitMcpOptions>()
            .BindConfiguration(GranitMcpOptions.SectionName);

        services.TryAddSingleton<McpMetrics>();
        services.TryAddSingleton<McpToolTypeRegistry>();

        // SDK: register MCP server
        IMcpServerBuilder mcpBuilder = services.AddMcpServer();

        // Bind ServerInfo from GranitMcpOptions via IConfigureOptions<McpServerOptions>
        services.AddSingleton<IConfigureOptions<McpServerOptions>>(sp =>
        {
            GranitMcpOptions granitOptions = sp.GetRequiredService<IOptions<GranitMcpOptions>>().Value;
            return new ConfigureOptions<McpServerOptions>(serverOptions =>
            {
                serverOptions.ServerInfo = new Implementation
                {
                    Name = granitOptions.ServerName,
                    Version = granitOptions.ServerVersion ?? "0.0.0",
                };
            });
        });

        // Sensitive property registry: scans loaded Granit assemblies for [SensitiveData].
        services.TryAddSingleton(_ =>
        {
            IEnumerable<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name?.StartsWith("Granit", StringComparison.Ordinal) == true);
            return new SensitivePropertyRegistry(assemblies);
        });

        // Default sanitizers: property redaction + response size limit
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMcpOutputSanitizer, PropertyRedactionSanitizer>());
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
            filters.AddListToolsFilter(next => async (context, ct) =>
                await ApplyVisibilityFilters(next, context, ct));
            filters.AddCallToolFilter(next => async (context, ct) =>
                await ApplyCallToolFilters(next, context, ct));
        });

        return mcpBuilder;
    }

    /// <summary>
    /// Scans module assemblies for MCP tools, prompts, and <see cref="IMcpToolContributor"/>
    /// implementations, then registers them with the MCP server.
    /// </summary>
    internal static IMcpServerBuilder DiscoverFromAssemblies(
        this IMcpServerBuilder mcpBuilder,
        IReadOnlyCollection<Assembly> moduleAssemblies,
        McpToolTypeRegistry toolTypeRegistry)
    {
        // Populate the tool type registry so visibility filters can resolve CLR types.
        toolTypeRegistry.RegisterFromAssemblies(moduleAssemblies);

        foreach (Assembly assembly in moduleAssemblies)
        {
            try
            {
                mcpBuilder.WithToolsFromAssembly(assembly);
                mcpBuilder.WithPromptsFromAssembly(assembly);
            }
            catch (ReflectionTypeLoadException)
            {
                // Assembly references internal types from another assembly via InternalsVisibleTo
                // (e.g. EFC modules consuming an internal DbContext). No MCP tools live in those
                // assemblies, so skipping is safe.
            }
        }

        foreach (Assembly assembly in moduleAssemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }

            IEnumerable<Type> contributorTypes = types
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

    private static async Task<ListToolsResult> ApplyVisibilityFilters(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next,
        RequestContext<ListToolsRequestParams> context,
        CancellationToken ct)
    {
        ListToolsResult result = await next(context, ct);

        IEnumerable<IMcpToolVisibilityFilter> visibilityFilters =
            context.Services!.GetServices<IMcpToolVisibilityFilter>();

        if (!visibilityFilters.Any())
        {
            return result;
        }

        List<Tool> visibleTools = [];
        foreach (Tool tool in result.Tools)
        {
            if (await IsToolVisible(tool, visibilityFilters, context.Services!, ct))
            {
                visibleTools.Add(tool);
            }
        }

        result.Tools = visibleTools;
        return result;
    }

    private static async Task<bool> IsToolVisible(
        Tool tool,
        IEnumerable<IMcpToolVisibilityFilter> filters,
        IServiceProvider services,
        CancellationToken ct)
    {
        // Resolve CLR type from the registry so visibility filters
        // (ExplicitDiscoveryFilter, TenantAwareVisibilityFilter) can inspect attributes.
        McpToolTypeRegistry? registry = services.GetService<McpToolTypeRegistry>();
        Type? toolType = registry?.Resolve(tool.Name);

        foreach (IMcpToolVisibilityFilter filter in filters)
        {
            if (!await filter.IsVisibleAsync(tool.Name, toolType, services, ct))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<CallToolResult> ApplyCallToolFilters(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> context,
        CancellationToken ct)
    {
        McpMetrics? metrics = context.Services?.GetService<McpMetrics>();
        ICurrentTenant? currentTenant = context.Services?.GetService<ICurrentTenant>();
        string? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id?.ToString() : null;
        string toolName = context.Params?.Name ?? "unknown";
        var sw = Stopwatch.StartNew();

        try
        {
            CallToolResult result = await next(context, ct);
            sw.Stop();

            result = await ApplySanitizers(result, context.Services!, ct);

            metrics?.RecordToolInvoked(tenantId, toolName, "success");
            metrics?.RecordRequestDuration(tenantId, $"tools/call/{toolName}", sw.Elapsed);

            return result;
        }
        catch (Exception)
        {
            sw.Stop();
            metrics?.RecordToolInvoked(tenantId, toolName, "error");
            metrics?.RecordRequestDuration(tenantId, $"tools/call/{toolName}", sw.Elapsed);
            throw;
        }
    }

    private static async Task<CallToolResult> ApplySanitizers(
        CallToolResult result,
        IServiceProvider services,
        CancellationToken ct)
    {
        IEnumerable<IMcpOutputSanitizer> sanitizers = services.GetServices<IMcpOutputSanitizer>();

        foreach (IMcpOutputSanitizer sanitizer in sanitizers)
        {
            result = await sanitizer.SanitizeAsync(result, services, ct);
        }

        return result;
    }
}
