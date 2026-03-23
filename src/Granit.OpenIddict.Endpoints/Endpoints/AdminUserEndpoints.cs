using Granit.Identity;
using Granit.Identity.Models;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Permissions;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using ImpersonationResult = Granit.OpenIddict.Services.ImpersonationResult;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AdminUserEndpoints
{
    internal static RouteGroupBuilder MapAdminUserEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder users = group.MapGroup("/users");

        users.MapGet("/", ListUsersAsync)
            .WithName("ListUsers")
            .WithSummary("Returns a paginated list of users.")
            .WithDescription("Supports search, pagination, and tenant filtering.")
            .Produces<IReadOnlyList<AdminUserResponse>>()
            .RequireAuthorization(OpenIddictPermissions.Users.Read);

        users.MapGet("/{userId:guid}", GetUserAsync)
            .WithName("GetUser")
            .WithSummary("Returns a user by ID.")
            .WithDescription("Returns the full user detail including roles and groups.")
            .Produces<AdminUserResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Users.Read);

        users.MapPost("/", CreateUserAsync)
            .WithName("CreateUser")
            .WithSummary("Creates a new user.")
            .WithDescription("Admin-initiated user creation. No email confirmation required.")
            .Produces<AdminUserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(OpenIddictPermissions.Users.Create);

        users.MapDelete("/{userId:guid}", DeleteUserAsync)
            .WithName("DeleteUser")
            .WithSummary("Soft-deletes a user.")
            .WithDescription("Sets IsDeleted = true and revokes all active tokens.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Users.Delete);

        users.MapPost("/{userId:guid}/impersonate", ImpersonateAsync)
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

    private static async Task<Ok<IReadOnlyList<AdminUserResponse>>> ListUsersAsync(
        [FromServices] IIdentityUserReader userReader,
        string? search = null, int page = 0, int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IIdentityUser> users = await userReader
            .GetUsersAsync(search, page * pageSize, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok<IReadOnlyList<AdminUserResponse>>(
            users.Select(MapToResponse).ToList());
    }

    private static async Task<Results<Ok<AdminUserResponse>, NotFound>> GetUserAsync(
        Guid userId,
        [FromServices] IIdentityUserReader userReader,
        CancellationToken cancellationToken = default)
    {
        IIdentityUser? user = await userReader
            .GetUserAsync(userId.ToString(), cancellationToken)
            .ConfigureAwait(false);

        return user is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(MapToResponse(user));
    }

    private static async Task<Created<AdminUserResponse>> CreateUserAsync(
        AdminUserCreateRequest request,
        [FromServices] IIdentityProvider identityProvider,
        CancellationToken cancellationToken = default)
    {
        IIdentityUser user = await identityProvider.CreateUserAsync(
            new IdentityUserCreate(
                request.Email,
                request.Email,
                request.FirstName,
                request.LastName,
                true,
                request.TemporaryPassword),
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/api/admin/users/{user.UserId}", MapToResponse(user));
    }

    private static AdminUserResponse MapToResponse(IIdentityUser user) =>
        new(user.UserId, user.Username, user.Email, user.FirstName, user.LastName,
            user.Enabled, user.ExtraProperties);

    private static async Task<Results<NoContent, NotFound>> DeleteUserAsync(
        Guid userId,
        [FromServices] IIdentityUserReader userReader,
        [FromServices] IAccountDeletionService deletionService,
        CancellationToken cancellationToken = default)
    {
        IIdentityUser? user = await userReader
            .GetUserAsync(userId.ToString(), cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        await deletionService.InitiateAsync(userId.ToString(), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
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

        // Event publication + metrics are handled by AspNetImpersonationService
        ImpersonationResult result = await impersonationService
            .ImpersonateAsync(userId.ToString(), adminId, adminName, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }
}
