using Granit.Identity.Local.Services;
using Granit.OpenIddict.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Endpoints;

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
            .Produces<ImpersonationResult>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Users.Impersonate);

        return group;
    }

    private static async Task<Results<Ok<ImpersonationResult>, ProblemHttpResult>> ImpersonateAsync(
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

        return TypedResults.Ok(result);
    }
}
