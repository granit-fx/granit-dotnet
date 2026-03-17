using Granit.Authorization.Abstractions;
using Granit.Authorization.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authorization.Endpoints.Endpoints;

/// <summary>
/// GET endpoint returning the permissions granted to the current authenticated user.
/// </summary>
internal static class MyPermissionsEndpoints
{
    /// <summary>
    /// Registers GET /me onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapMyPermissionsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me", GetMyPermissionsAsync)
            .WithName("GetMyPermissions")
            .WithSummary("Returns the list of permissions granted to the current user.")
            .WithDescription("Evaluates all registered permission definitions against the current user's claims and roles. Returns the flat list of granted permission names. Useful for front-end UI to conditionally render actions based on the user's effective permissions.")
            .RequireAuthorization();

        return group;
    }

    private static async Task<Ok<MyPermissionsResponse>> GetMyPermissionsAsync(
        IPermissionDefinitionManager definitionManager,
        IPermissionChecker permissionChecker,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PermissionDefinition> allPermissions = definitionManager.GetAll();
        List<string> granted = [];

        foreach (string permissionName in allPermissions.Select(permission => permission.Name))
        {
            if (await permissionChecker.IsGrantedAsync(permissionName, cancellationToken).ConfigureAwait(false))
            {
                granted.Add(permissionName);
            }
        }

        return TypedResults.Ok(new MyPermissionsResponse(granted));
    }
}
