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
    /// Does NOT call <c>AddMcpServer()</c> — that is done by <see cref="Granit.Mcp.GranitMcpModule"/>.
    /// This method chains <c>WithHttpTransport()</c> and <c>AddAuthorizationFilters()</c>
    /// onto the already-registered MCP server builder.
    /// </remarks>
    internal static IServiceCollection AddGranitMcpServer(this IServiceCollection services)
    {
        services
            .AddOptions<GranitMcpServerOptions>()
            .BindConfiguration(GranitMcpServerOptions.SectionName);

        // SDK: chain HTTP transport + auth onto the existing MCP server (registered by GranitMcpModule)
        services
            .AddMcpServer()
            .WithHttpTransport()
            .AddAuthorizationFilters();

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
