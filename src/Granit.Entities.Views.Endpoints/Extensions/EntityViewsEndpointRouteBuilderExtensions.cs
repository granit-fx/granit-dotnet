using Granit.Entities.Views.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Entities.Views.Endpoints.Extensions;

/// <summary>
/// Route-builder extensions for the EntityView HTTP surface.
/// </summary>
public static class EntityViewsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Mounts the EntityView routes under <c>{prefix}/{entityName}/views</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="prefix">Route prefix (typically <c>"/api/{version}/entities"</c>).</param>
    /// <param name="configure">Optional <see cref="EntityViewsEndpointsOptions"/> hook.</param>
    /// <returns>The created <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitEntityViewsEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix,
        Action<EntityViewsEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        EntityViewsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(prefix + "/{entityName}/views")
            .WithTags(options.TagName)
            .RequireAuthorization(EntityViewPermissions.Read);

        group.MapEntityViewsEndpoints();

        return group;
    }
}
