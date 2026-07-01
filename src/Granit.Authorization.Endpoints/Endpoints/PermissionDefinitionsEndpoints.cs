using Granit.Authorization.Endpoints.Dtos;
using Granit.Authorization.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Granit.Authorization.Endpoints.Endpoints;

/// <summary>
/// GET endpoint returning all registered permission definitions grouped by category.
/// Display names are resolved via <see cref="IStringLocalizerFactory"/> when available,
/// returning localized strings based on the current request culture.
/// </summary>
internal static class PermissionDefinitionsEndpoints
{
    /// <summary>
    /// Registers GET /definitions onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapPermissionDefinitionsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/definitions", GetDefinitions)
            .WithName("GetPermissionDefinitions")
            .WithSummary("Returns all registered permission definitions grouped by category.")
            .WithDescription("Returns the full registry of permission definitions discovered from all loaded modules, organized by permission group. Display names are localized based on the Accept-Language header. This endpoint is intended for admin UIs that manage role-to-permission assignments.")
            .Produces<IReadOnlyList<PermissionGroupResponse>>()
            .RequireAuthorization(AuthorizationEndpointsPermissions.Definitions.Read);

        return group;
    }

    private static Ok<IReadOnlyList<PermissionGroupResponse>> GetDefinitions(
        [FromServices] IPermissionDefinitionRegistry definitionManager,
        HttpContext httpContext)
    {
        IStringLocalizerFactory? localizerFactory =
            httpContext.RequestServices.GetService<IStringLocalizerFactory>();

        IReadOnlyList<PermissionGroup> groups = definitionManager.GetGroups();

        var response = groups
            .Select(g => new PermissionGroupResponse(
                g.Name,
                g.DisplayName?.Localize(localizerFactory),
                g.Permissions
                    .Select(p => new PermissionDefinitionResponse(
                        p.Name,
                        p.DisplayName?.Localize(localizerFactory),
                        p.MultiTenancySides))
                    .ToList()))
            .ToList();

        return TypedResults.Ok<IReadOnlyList<PermissionGroupResponse>>(response);
    }
}
