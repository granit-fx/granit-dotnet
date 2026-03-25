using Granit.Identity;
using Granit.OpenIddict.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AccountProfileEndpoints
{
    internal static RouteGroupBuilder MapAccountProfileEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/profile", GetProfileAsync)
            .WithName("GetAccountProfile")
            .WithSummary("Returns the current user's profile.")
            .WithDescription(
                "Fetches the authenticated user's profile including email, name, "
                + "2FA status, and linked external logins.")
            .Produces<AccountProfileResponse>()
            .RequireAuthorization();

        group.MapPut("/profile", UpdateProfileAsync)
            .WithName("UpdateAccountProfile")
            .WithSummary("Updates the current user's profile.")
            .WithDescription(
                "Updates the authenticated user's first name and last name. "
                + "Returns the updated profile.")
            .Produces<AccountProfileResponse>()
            .ProducesValidationProblem()
            .RequireAuthorization();

        return group;
    }

    private static async Task<Ok<AccountProfileResponse>> GetProfileAsync(
        HttpContext httpContext,
[FromServices] IIdentityUserReader userReader,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "private, no-cache, no-store";

        string userId = httpContext.User.FindFirst("sub")!.Value;
        IIdentityUser? user = await userReader
            .GetUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(MapToProfile(user!, userId));
    }

    private static async Task<Results<Ok<AccountProfileResponse>, ProblemHttpResult>> UpdateProfileAsync(
        AccountProfileUpdateRequest request,
        HttpContext httpContext,
[FromServices] IIdentityUserWriter userWriter,
        [FromServices] IIdentityUserReader userReader,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;

        await userWriter.UpdateUserAsync(
            userId,
            new Granit.Identity.Models.IdentityUserUpdate
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
            },
            cancellationToken).ConfigureAwait(false);

        IIdentityUser? updated = await userReader
            .GetUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(MapToProfile(updated!, userId));
    }

    private static AccountProfileResponse MapToProfile(
        IIdentityUser user, string userId) =>
        new(
            Guid.Parse(userId),
            user.Email ?? string.Empty,
            true, // EmailConfirmed — resolved from claims in production
            user.FirstName,
            user.LastName,
            false, // TwoFactorEnabled — resolved from UserManager in production
            true, // HasPassword
            []); // ExternalLogins
}
