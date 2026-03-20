using Granit.Identity.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Endpoints for managing user passwords via the identity provider.
/// </summary>
internal static class IdentityProviderPasswordEndpoints
{
    internal static RouteGroupBuilder MapProviderPasswordEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/changed-at", GetPasswordChangedAtAsync)
            .WithName("GetIdentityProviderPasswordChangedAt")
            .WithSummary("Returns the timestamp of the user's last password change.")
            .WithDescription("Returns the date and time of the user's last password change, or null if the information is not available.")
            .Produces<IdentityPasswordChangedAtResponse>();

        group.MapPost("/reset-email", SendPasswordResetEmailAsync)
            .WithName("SendIdentityProviderPasswordResetEmail")
            .WithSummary("Sends a password reset email to the user.")
            .WithDescription("Triggers a password reset email via the identity provider. Returns 501 if the provider does not support native password reset emails.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPost("/temporary", SetTemporaryPasswordAsync)
            .WithName("SetIdentityProviderTemporaryPassword")
            .WithSummary("Sets a temporary password for the user.")
            .WithDescription("Sets a temporary password that the user must change on next login. Useful for admin-initiated password resets.")
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Ok<IdentityPasswordChangedAtResponse>> GetPasswordChangedAtAsync(
        string userId,
        [FromServices] IIdentityPasswordManager passwordManager,
        CancellationToken cancellationToken)
    {
        DateTimeOffset? changedAt = await passwordManager
            .GetPasswordChangedAtAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new IdentityPasswordChangedAtResponse(changedAt));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> SendPasswordResetEmailAsync(
        string userId,
        [FromServices] IIdentityPasswordManager passwordManager,
        [FromServices] IIdentityProviderCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        if (!capabilities.SupportsNativePasswordResetEmail)
        {
            return TypedResults.Problem(
                detail: $"The '{capabilities.ProviderName}' provider does not support native password reset emails.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        await passwordManager.SendPasswordResetEmailAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> SetTemporaryPasswordAsync(
        string userId,
        IdentitySetTemporaryPasswordRequest request,
        [FromServices] IIdentityPasswordManager passwordManager,
        CancellationToken cancellationToken)
    {
        await passwordManager.SetTemporaryPasswordAsync(userId, request.Password, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
