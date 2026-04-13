using Granit.Http.Idempotency.Attributes;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Identity.Local.Endpoints.Permissions;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Local.Endpoints.Endpoints;

internal static class AdminImpersonationEndpoints
{
    internal static RouteGroupBuilder MapAdminImpersonationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/users/{userId:guid}/impersonate", ImpersonateAsync)
            .WithName("ImpersonateUser")
            .WithSummary("Impersonates a user.")
            .WithDescription(
                "Issues a short-lived token (max 1h) with impersonator_id claim. "
                + "Writes audit log and sends transparency notification.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<ImpersonationResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(IdentityLocalPermissions.Users.Impersonate);

        return group;
    }

    private static async Task<Results<Ok<ImpersonationResponse>, ProblemHttpResult>> ImpersonateAsync(
        Guid userId,
        HttpContext httpContext,
        [FromServices] IImpersonationService impersonationService,
        CancellationToken cancellationToken = default)
    {
        // Guard: cannot chain-impersonate
        if (httpContext.User.FindFirst("impersonator_id") is not null)
        {
            return TypedResults.Problem(
                detail: "Cannot impersonate while already impersonating.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        string adminId = httpContext.User.FindFirst("sub")!.Value;
        string adminName = httpContext.User.FindFirst("email")?.Value
                           ?? httpContext.User.FindFirst("name")?.Value
                           ?? adminId;

        ImpersonationResult result = await impersonationService
            .ImpersonateAsync(userId.ToString(), adminId, adminName, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(IdentityLocalResponseMapper.ToResponse(result));
    }
}
