using Granit.Entities.Customization.Endpoints.Options;
using Granit.Entities.Customization.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Entities.Customization.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping the entities-customization endpoints onto an
/// <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class EntitiesCustomizationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps GET / PUT / DELETE for the per-tenant Layer 1 customization on
    /// each <c>EntityDefinition</c> layout. The route group requires
    /// <c>EntitiesCustomization.Customizations.Read</c>; PUT and DELETE
    /// layer the <c>Manage</c> permission on top.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize options.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitEntitiesCustomization(
        this IEndpointRouteBuilder endpoints,
        Action<EntitiesCustomizationEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        EntitiesCustomizationEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(EntitiesCustomizationPermissions.Customizations.Read);

        group.MapEntityCustomizationEndpoints();
        return group;
    }
}
