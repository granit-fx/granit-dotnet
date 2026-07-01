using System.Diagnostics.CodeAnalysis;
using Granit.Mcp.Sanitization;
using Granit.Mcp.Server.Internal;
using Granit.Mcp.Server.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Mcp.Server.Extensions;

/// <summary>
/// Extension methods for registering the Granit MCP server services.
/// Called by <see cref="GranitMcpServerModule.ConfigureServices"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public static class McpServerServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit MCP server services: HTTP transport, SDK authorization filters,
    /// tool visibility filters, and error sanitization.
    /// </summary>
    /// <remarks>
    /// Calls <c>AddMcpServer()</c> to obtain the builder (idempotent — the SDK uses
    /// <c>TryAdd</c> internally, so the server registered by <see cref="Granit.Mcp.GranitMcpModule"/>
    /// is reused). Chains <c>WithHttpTransport()</c> and <c>AddAuthorizationFilters()</c>
    /// onto that builder.
    /// </remarks>
    internal static IServiceCollection AddGranitMcpServer(this IServiceCollection services)
    {
        services
            .AddOptions<GranitMcpServerOptions>()
            .BindConfiguration(GranitMcpServerOptions.SectionName);

        // SDK: chain HTTP transport + auth onto the existing MCP server (registered by GranitMcpModule).
        // The default-deny call-tool gate is registered first so it wraps the base module's
        // metrics/sanitizer call-tool filter and rejects un-annotated tools before dispatch.
        services
            .AddMcpServer()
            .WithHttpTransport()
            .AddAuthorizationFilters()
            .WithRequestFilters(filters =>
                filters.AddCallToolFilter(CallToolAuthorizationFilter.Wrap));

        // Granit visibility filters
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMcpToolVisibilityFilter, ExplicitDiscoveryFilter>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMcpToolVisibilityFilter, TenantAwareVisibilityFilter>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMcpToolVisibilityFilter, ModuleScopeVisibilityFilter>());

        // Error sanitizer
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMcpOutputSanitizer, ErrorSanitizer>());

        return services;
    }
}
