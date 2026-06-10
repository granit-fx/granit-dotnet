using Granit.Http.Idempotency.Attributes;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Local.Endpoints.Endpoints;

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
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<AccountProfileResponse>()
            .ProducesValidationProblem()
            .RequireAuthorization();

        return group;
    }

    private static async Task<Ok<AccountProfileResponse>> GetProfileAsync(
        HttpContext httpContext,
        [FromServices] IIdentityUserReader userReader,
        [FromServices] ITwoFactorService twoFactorService,
        [FromServices] IExternalLoginService externalLoginService,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "private, no-cache, no-store";

        string userId = httpContext.User.FindFirst("sub")!.Value;
        IIdentityUser? user = await userReader
            .GetUserAsync(userId, cancellationToken).ConfigureAwait(false);

        AccountProfileResponse profile = await MapToProfileAsync(
            user!, userId, httpContext, twoFactorService, externalLoginService, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(profile);
    }

    private static async Task<Results<Ok<AccountProfileResponse>, ProblemHttpResult>> UpdateProfileAsync(
        AccountProfileUpdateRequest request,
        HttpContext httpContext,
        [FromServices] IIdentityUserWriter userWriter,
        [FromServices] IIdentityUserReader userReader,
        [FromServices] ITwoFactorService twoFactorService,
        [FromServices] IExternalLoginService externalLoginService,
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

        AccountProfileResponse profile = await MapToProfileAsync(
            updated!, userId, httpContext, twoFactorService, externalLoginService, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(profile);
    }

    private static async Task<AccountProfileResponse> MapToProfileAsync(
        IIdentityUser user,
        string userId,
        HttpContext httpContext,
        ITwoFactorService twoFactorService,
        IExternalLoginService externalLoginService,
        CancellationToken cancellationToken)
    {
        // Resolve email_verified from token claims (standard OIDC claim)
        bool emailConfirmed = httpContext.User.FindFirst("email_verified")?.Value is "true";

        TwoFactorStatus twoFactorStatus = await twoFactorService
            .GetStatusAsync(userId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ExternalLoginInfo> externalLogins = await externalLoginService
            .GetLoginsAsync(userId, cancellationToken).ConfigureAwait(false);

        return new AccountProfileResponse(
            Guid.Parse(userId),
            user.Email ?? string.Empty,
            emailConfirmed,
            user.FirstName,
            user.LastName,
            twoFactorStatus.IsEnabled,
            null, // HasPassword — no abstraction exposes it without a UserManager dependency; resolved by the provider layer
            externalLogins.Select(l => l.LoginProvider).ToList());
    }
}
