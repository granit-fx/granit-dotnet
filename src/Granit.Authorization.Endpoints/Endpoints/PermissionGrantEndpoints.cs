using System.Text.RegularExpressions;
using Granit.Authorization;
using Granit.Authorization.Endpoints.Dtos;
using Granit.Authorization.Endpoints.Permissions;
using Granit.MultiTenancy;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authorization.Endpoints.Endpoints;

/// <summary>
/// Admin endpoints for viewing and managing role → permission grants.
/// </summary>
internal static partial class PermissionGrantEndpoints
{
    /// <summary>
    /// Registers GET /roles/{roleName}, PUT /roles/{roleName}/{permissionName},
    /// and DELETE /roles/{roleName}/{permissionName} onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapPermissionGrantEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder adminGroup = group.MapGranitGroup("/roles")
            .RequireAuthorization(AuthorizationEndpointsPermissions.Grants.Manage);

        adminGroup.MapGet("/{roleName}", GetGrantedPermissionsAsync)
            .WithName("GetRolePermissions")
            .WithSummary("Returns the list of permissions explicitly granted to a role.")
            .WithDescription("Returns only the permissions explicitly assigned to the specified role for the current tenant. Does not include inherited or implicit permissions. The role name is case-sensitive and must match the identity provider's role definition.")
            .Produces<PermissionGrantResponse>();

        adminGroup.MapPut("/{roleName}/{permissionName}", GrantPermissionAsync)
            .WithName("GrantPermission")
            .WithSummary("Grants a permission to a role. No-op if already granted.")
            .WithDescription("Grants the specified permission to the role for the current tenant. The permission name must match a registered permission definition (returns 422 otherwise). The calling user must hold the permission being granted (privilege escalation prevention). Idempotent — granting an already-granted permission is a no-op.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        adminGroup.MapDelete("/{roleName}/{permissionName}", RevokePermissionAsync)
            .WithName("RevokePermission")
            .WithSummary("Revokes a permission from a role. No-op if not granted.")
            .WithDescription("Revokes the specified permission from the role for the current tenant. The permission name must match a registered permission definition (returns 422 otherwise). The calling user must hold the permission being revoked (privilege escalation prevention). Idempotent — revoking a non-granted permission is a no-op.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<Results<Ok<PermissionGrantResponse>, ValidationProblem>> GetGrantedPermissionsAsync(
        string roleName,
        [FromServices] IPermissionManagerReader permissionManagerReader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!IsValidName(roleName))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["roleName"] = ["Role name must be 1-256 characters (alphanumeric, dots, hyphens, underscores)."]
                });
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        IReadOnlyList<string> permissions = await permissionManagerReader
            .GetGrantedPermissionsAsync(roleName, tenantId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new PermissionGrantResponse(roleName, permissions));
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> GrantPermissionAsync(
        string roleName,
        string permissionName,
        [FromServices] IPermissionManagerWriter permissionManagerWriter,
        [FromServices] IPermissionDefinitionManager definitionManager,
        [FromServices] IPermissionChecker permissionChecker,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!IsValidName(roleName) || !IsValidName(permissionName))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["name"] = ["Role and permission names must be 1-256 characters (alphanumeric, dots, hyphens, underscores)."]
                });
        }

        if (!definitionManager.Exists(permissionName))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["permissionName"] = ["The specified permission is not registered."]
                });
        }

        // VULN-100 fix: prevent privilege escalation — callers can only grant
        // permissions they themselves hold (or are in an AdminRole).
        if (!await permissionChecker.IsGrantedAsync(permissionName, cancellationToken).ConfigureAwait(false))
        {
            return TypedResults.Problem(
                detail: "Cannot grant a permission that the current user does not hold.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        await permissionManagerWriter.SetAsync(permissionName, roleName, tenantId, isGranted: true, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> RevokePermissionAsync(
        string roleName,
        string permissionName,
        [FromServices] IPermissionManagerWriter permissionManagerWriter,
        [FromServices] IPermissionDefinitionManager definitionManager,
        [FromServices] IPermissionChecker permissionChecker,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!IsValidName(roleName) || !IsValidName(permissionName))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["name"] = ["Role and permission names must be 1-256 characters (alphanumeric, dots, hyphens, underscores)."]
                });
        }

        if (!definitionManager.Exists(permissionName))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["permissionName"] = ["The specified permission is not registered."]
                });
        }

        // VULN-101 fix: prevent privilege escalation — callers can only revoke
        // permissions they themselves hold (symmetric with GrantPermissionAsync).
        if (!await permissionChecker.IsGrantedAsync(permissionName, cancellationToken).ConfigureAwait(false))
        {
            return TypedResults.Problem(
                detail: "Cannot revoke a permission that the current user does not hold.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        await permissionManagerWriter.SetAsync(permissionName, roleName, tenantId, isGranted: false, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static bool IsValidName(string name) =>
        name.Length is > 0 and <= 256 && ValidNameRegex().IsMatch(name);

    [GeneratedRegex(@"^[\w.\-]{1,256}$")]
    private static partial Regex ValidNameRegex();
}
