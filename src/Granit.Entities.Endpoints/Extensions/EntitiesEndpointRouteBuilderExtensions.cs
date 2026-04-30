using Granit.Entities.Endpoints.Endpoints;
using Granit.Entities.Endpoints.Internal;
using Granit.Entities.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Entities.Endpoints.Extensions;

/// <summary>
/// Route-builder extensions for the entity-manifest HTTP surface.
/// </summary>
public static class EntitiesEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Mounts <c>GET /api/entities</c> (discovery tree) and
    /// <c>GET /api/entities/{name}</c> (per-entity manifest) under the configured
    /// prefix.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="prefix">Route prefix (typically <c>"/api/{version}/entities"</c>).</param>
    /// <param name="configure">Optional <see cref="EntitiesEndpointsOptions"/> hook.</param>
    /// <returns>The created <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitEntitiesEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix,
        Action<EntitiesEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        EntitiesEndpointsOptions options = new();
        configure?.Invoke(options);

        // Defense-in-depth happens per entity inside the handler. The route
        // group itself only requires authentication — anonymous discovery
        // would expose the registered entity catalogue, which is sensitive in
        // multi-tenant deployments.
        RouteGroupBuilder group = endpoints
            .MapGranitGroup(prefix)
            .WithTags(options.TagName)
            .RequireAuthorization();

        group.MapEntitiesEndpoints();

        return group;
    }

    /// <summary>
    /// Registers the <see cref="EntityPermissionResolver"/> service required by
    /// the manifest handlers. Called once per host from the framework's
    /// service-collection extension.
    /// </summary>
    public static IServiceCollection AddGranitEntitiesEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<EntityPermissionResolver>();
        services.AddOptions<EntitiesEndpointsOptions>();
        return services;
    }
}
