using Granit.Authorization.Abstractions;
using Granit.Authorization.Endpoints.Dtos;
using Granit.Authorization.Endpoints.Permissions;
using Granit.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authorization.Endpoints.Endpoints;

/// <summary>
/// Admin endpoints for viewing and managing role → permission grants.
/// </summary>
internal static class PermissionGrantEndpoints
{
    /// <summary>
    /// Registers GET /roles/{roleName}, PUT /roles/{roleName}/{permissionName},
    /// and DELETE /roles/{roleName}/{permissionName} onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapPermissionGrantEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder adminGroup = group.MapGroup("/roles")
            .RequireAuthorization(AuthorizationEndpointsPermissions.Grants.Manage);

        adminGroup.MapGet("/{roleName}", GetGrantedPermissionsAsync)
            .WithName("GetRolePermissions")
            .WithSummary("Returns the list of permissions explicitly granted to a role.")
            .WithDescription("Returns only the permissions explicitly assigned to the specified role for the current tenant. Does not include inherited or implicit permissions. The role name is case-sensitive and must match the identity provider's role definition.");

        adminGroup.MapPut("/{roleName}/{permissionName}", GrantPermissionAsync)
            .WithName("GrantPermission")
            .WithSummary("Grants a permission to a role. No-op if already granted.")
            .WithDescription("Grants the specified permission to the role for the current tenant. The permission name must match a registered permission definition (returns 422 otherwise). Idempotent — granting an already-granted permission is a no-op.");

        adminGroup.MapDelete("/{roleName}/{permissionName}", RevokePermissionAsync)
            .WithName("RevokePermission")
            .WithSummary("Revokes a permission from a role. No-op if not granted.")
            .WithDescription("Revokes the specified permission from the role for the current tenant. The permission name must match a registered permission definition (returns 422 otherwise). Idempotent — revoking a non-granted permission is a no-op.");

        return group;
    }

    private static async Task<Ok<PermissionGrantResponse>> GetGrantedPermissionsAsync(
        string roleName,
        [FromServices] IPermissionManagerReader permissionManagerReader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        IReadOnlyList<string> permissions = await permissionManagerReader
            .GetGrantedPermissionsAsync(roleName, tenantId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new PermissionGrantResponse(roleName, permissions));
    }

    private static async Task<Results<NoContent, ValidationProblem>> GrantPermissionAsync(
        string roleName,
        string permissionName,
        [FromServices] IPermissionManagerWriter permissionManagerWriter,
        [FromServices] IPermissionDefinitionManager definitionManager,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!definitionManager.Exists(permissionName))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["permissionName"] = [$"Permission '{permissionName}' is not defined."]
                });
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        await permissionManagerWriter.SetAsync(permissionName, roleName, tenantId, isGranted: true, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ValidationProblem>> RevokePermissionAsync(
        string roleName,
        string permissionName,
        [FromServices] IPermissionManagerWriter permissionManagerWriter,
        [FromServices] IPermissionDefinitionManager definitionManager,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!definitionManager.Exists(permissionName))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["permissionName"] = [$"Permission '{permissionName}' is not defined."]
                });
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        await permissionManagerWriter.SetAsync(permissionName, roleName, tenantId, isGranted: false, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
