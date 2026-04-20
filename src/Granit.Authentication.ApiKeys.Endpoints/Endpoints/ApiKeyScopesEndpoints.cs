using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Authorization;
using Granit.Http.Idempotency.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authentication.ApiKeys.Endpoints.Endpoints;

/// <summary>
/// Endpoint for updating API key permissions and CIDR scopes.
/// </summary>
internal static class ApiKeyScopesEndpoints
{
    internal static RouteGroupBuilder MapScopesEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}/scopes", UpdateScopesAsync)
            .WithName("UpdateApiKeyScopes")
            .WithSummary("Updates the permissions and allowed CIDR ranges for an API key.")
            .WithDescription("Replaces the full list of permissions and allowed CIDR ranges for the specified key. The caller must possess every permission being assigned (privilege escalation prevention). Both fields are replaced entirely (not merged). Returns 404 if the key does not exist.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UpdateScopesAsync(
        Guid id,
        ApiKeyUpdateScopesRequest request,
        [FromServices] IApiKeyAdminStore adminStore,
        [FromServices] IPermissionChecker permissionChecker,
        CancellationToken cancellationToken)
    {
        // Validate that the caller possesses every permission being assigned (OWASP API5:2023)
        foreach (string permission in request.Permissions)
        {
            if (!await permissionChecker.IsGrantedAsync(permission, cancellationToken).ConfigureAwait(false))
            {
                return TypedResults.Problem(
                    detail: $"Cannot assign permission '{permission}' — caller does not possess it.",
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        bool updated = await adminStore.UpdateScopesAsync(
            id,
            request.Permissions,
            request.AllowedCidrs,
            cancellationToken).ConfigureAwait(false);

        if (!updated)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.NoContent();
    }
}
