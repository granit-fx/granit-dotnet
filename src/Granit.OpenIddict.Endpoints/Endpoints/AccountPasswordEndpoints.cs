using Granit.Identity;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AccountPasswordEndpoints
{
    internal static RouteGroupBuilder MapAccountPasswordEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/change-password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .WithSummary("Changes the authenticated user's password.")
            .WithDescription(
                "Validates the current password, then sets the new password. "
                + "Returns 400 if the current password is incorrect or the new password is too weak.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .WithSummary("Sends a password reset email.")
            .WithDescription(
                "Sends a password reset link to the specified email if the account exists. "
                + "Always returns 202 to prevent user enumeration.")
            .Produces(StatusCodes.Status202Accepted)
            .AllowAnonymous();

        group.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("ResetPassword")
            .WithSummary("Resets a user's password using a reset token.")
            .WithDescription(
                "Validates the reset token from the email link and sets the new password. "
                + "Returns 400 if the token is invalid or expired.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .AllowAnonymous();

        return group;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangePasswordAsync(
        AccountPasswordChangeRequest request,
        HttpContext httpContext,
[FromServices] IIdentityCredentialVerifier credentialVerifier,
[FromServices] IIdentityPasswordManager passwordManager,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        string? username = httpContext.User.FindFirst("preferred_username")?.Value
                           ?? httpContext.User.FindFirst("name")?.Value;

        bool isValid = await credentialVerifier
            .VerifyUserCredentialsAsync(username ?? string.Empty, request.CurrentPassword, cancellationToken)
            .ConfigureAwait(false);

        if (!isValid)
        {
            return TypedResults.Problem(
                detail: "Current password is incorrect.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await passwordManager.SetTemporaryPasswordAsync(userId, request.NewPassword, cancellationToken)
            .ConfigureAwait(false);

        httpContext.RequestServices.GetService<OpenIddictMetrics>()?.RecordPasswordChange(null);
        return TypedResults.NoContent();
    }

    private static async Task<Accepted<string>> ForgotPasswordAsync(
        AccountForgotPasswordRequest request,
        [FromServices] IPasswordResetService passwordResetService,
        CancellationToken cancellationToken)
    {
        // Always return 202 regardless of whether the email exists (prevents enumeration).
        // IPasswordResetService.RequestResetAsync publishes PasswordResetRequestedEto
        // which a subscriber (Granit.Notifications or app-level) consumes to send the email.
        await passwordResetService.RequestResetAsync(request.Email, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Accepted((string?)null, (string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ResetPasswordAsync(
        AccountPasswordResetRequest request,
        [FromServices] IPasswordResetService passwordResetService,
        CancellationToken cancellationToken)
    {
        try
        {
            await passwordResetService.ResetPasswordAsync(
                request.UserId, request.Token, request.NewPassword, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Problem(
                detail: "Invalid or expired reset token.",
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
