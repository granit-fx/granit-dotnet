using System.Diagnostics.CodeAnalysis;
using Granit.Mcp.Sanitization;
using Granit.Mcp.Server.Internal;
using Granit.Mcp.Server.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Mcp.Server.Extensions;

/// <summary>
/// Extension methods for registering the Granit MCP server (HTTP transport + auth).
/// </summary>
[ExcludeFromCodeCoverage]
public static class McpServerHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Granit MCP server services: HTTP transport, SDK authorization filters,
    /// tool visibility filters, and error sanitization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call <c>MapGranitMcpServer()</c> on the <c>WebApplication</c> to map the
    /// MCP endpoint. The SDK's <c>.AddAuthorizationFilters()</c> enables standard
    /// <c>[Authorize(Policy = "...")]</c> on tool classes and methods.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitMcpServer(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<GranitMcpServerOptions>()
            .BindConfiguration(GranitMcpServerOptions.SectionName);

        // SDK: HTTP transport (Streamable HTTP + legacy SSE)
        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .AddAuthorizationFilters();

        // Granit visibility filters
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMcpToolVisibilityFilter, ExplicitDiscoveryFilter>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMcpToolVisibilityFilter, TenantAwareVisibilityFilter>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMcpToolVisibilityFilter, ModuleScopeVisibilityFilter>());

        // Error sanitizer
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMcpOutputSanitizer, ErrorSanitizer>());

        return builder;
    }
}
