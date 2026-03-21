using Granit.Identity;
using Granit.OpenIddict.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

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
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization(OpenIddictPermissions.Users.Read);

        users.MapGet("/{userId:guid}", GetUserAsync)
            .WithName("GetUser")
            .WithSummary("Returns a user by ID.")
            .WithDescription("Returns the full user detail including roles and groups.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Users.Read);

        users.MapPost("/", CreateUserAsync)
            .WithName("CreateUser")
            .WithSummary("Creates a new user.")
            .WithDescription("Admin-initiated user creation. No email confirmation required.")
            .Produces(StatusCodes.Status201Created)
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
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Users.Impersonate);

        return group;
    }

    private static Task<Ok> ListUsersAsync(
[FromServices] IIdentityUserReader userReader,
        string? search = null, int page = 0, int pageSize = 20)
    {
        // TODO: Implement with IIdentityUserReader.GetUsersAsync()
        return Task.FromResult(TypedResults.Ok());
    }

    private static Task<Results<Ok, NotFound>> GetUserAsync(
        Guid userId,
[FromServices] IIdentityUserReader userReader)
    {
        // TODO: Implement with IIdentityUserReader.GetUserAsync()
        return Task.FromResult<Results<Ok, NotFound>>(TypedResults.Ok());
    }

    private static Task<Created> CreateUserAsync(
        [FromServices] IIdentityUserWriter userWriter)
    {
        // TODO: Implement with IIdentityProvider.CreateUserAsync()
        return Task.FromResult(TypedResults.Created("/api/admin/users/{id}"));
    }

    private static Task<Results<NoContent, NotFound>> DeleteUserAsync(
        Guid userId,
        [FromServices] IIdentityUserWriter userWriter)
    {
        // TODO: Implement soft-delete
        return Task.FromResult<Results<NoContent, NotFound>>(TypedResults.NoContent());
    }

    private static Task<Results<Ok, ProblemHttpResult>> ImpersonateAsync(
        Guid userId,
        HttpContext httpContext)
    {
        // TODO: Implement impersonation (Feature #391)
        return Task.FromResult<Results<Ok, ProblemHttpResult>>(TypedResults.Ok());
    }
}
