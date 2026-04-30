using Granit.Validation.AspNetCore;
using Granit.Workspaces.Endpoints.Endpoints;
using Granit.Workspaces.Endpoints.Internal;
using Granit.Workspaces.Endpoints.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Workspaces.Endpoints.Extensions;

/// <summary>
/// Route-builder extensions for the workspace tree HTTP surface.
/// </summary>
public static class WorkspacesEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Mounts <c>GET /api/workspaces</c> under the configured prefix.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="prefix">Route prefix (typically <c>"/api/{version}/workspaces"</c>).</param>
    /// <param name="configure">Optional <see cref="WorkspacesEndpointsOptions"/> hook.</param>
    /// <returns>The created <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitWorkspacesEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix,
        Action<WorkspacesEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        WorkspacesEndpointsOptions options = new();
        configure?.Invoke(options);

        // The tree filter handles per-workspace / per-item gating; the route
        // group only requires authentication to keep anonymous traffic from
        // probing the registered workspace catalogue.
        RouteGroupBuilder group = endpoints
            .MapGranitGroup(prefix)
            .WithTags(options.TagName)
            .RequireAuthorization();

        group.MapWorkspacesEndpoints();
        return group;
    }

    /// <summary>
    /// Registers the <see cref="WorkspaceFilter"/> service required by the
    /// workspace tree handler. Called once from the framework's
    /// service-collection extension.
    /// </summary>
    public static IServiceCollection AddGranitWorkspacesEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<WorkspaceFilter>();
        services.AddOptions<WorkspacesEndpointsOptions>();
        return services;
    }
}
