using System.Diagnostics;
using Granit.Events;
using Granit.Http.Idempotency.Attributes;
using Granit.Http.Timing;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Local.Endpoints.Endpoints;

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
            .WithMetadata(new IdempotentAttribute { Required = false })
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
            .AllowAnonymous()
            .RequireRateLimiting("authentication");

        group.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("ResetPassword")
            .WithSummary("Resets a user's password using a reset token.")
            .WithDescription(
                "Validates the reset token from the email link and sets the new password. "
                + "Returns 400 if the token is invalid or expired.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .AllowAnonymous()
            .RequireRateLimiting("authentication");

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

        if (username is null)
        {
            return TypedResults.Problem(
                detail: "Unable to determine username from token claims.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        bool isValid = await credentialVerifier
            .VerifyUserCredentialsAsync(username, request.CurrentPassword, cancellationToken)
            .ConfigureAwait(false);

        if (!isValid)
        {
            return TypedResults.Problem(
                detail: "Current password is incorrect.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await passwordManager.SetTemporaryPasswordAsync(userId, request.NewPassword, cancellationToken)
            .ConfigureAwait(false);

        string? tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
        Guid? parsedTenantId = tenantId is not null ? Guid.Parse(tenantId) : null;

        await PublishPasswordChangedAsync(httpContext, Guid.Parse(userId), parsedTenantId, cancellationToken)
            .ConfigureAwait(false);

        httpContext.RequestServices.GetService<IdentityLocalMetrics>()?.RecordPasswordChange(tenantId);
        return TypedResults.NoContent();
    }

    private static async Task<Accepted<string>> ForgotPasswordAsync(
        AccountForgotPasswordRequest request,
        [FromServices] IPasswordResetService passwordResetService,
        CancellationToken cancellationToken)
    {
        // Pad the response to the same 500-700 ms floor as login. Without this, an
        // attacker can enumerate registered emails by timing: a hit hashes a token
        // and writes an outbox row, a miss returns immediately. Reuse the login
        // bounds so /login and /forgot-password present the same surface.
        await using var floor = MinimumResponseTimeGuard.Begin(
            AccountLoginEndpoints.MinResponseFloorMs, AccountLoginEndpoints.MaxResponseFloorMs);

        using Activity? activity = IdentityLocalActivitySource.Source.StartActivity(
            IdentityLocalActivitySource.PasswordReset);
        activity?.SetTag(IdentityLocalActivitySource.TagProvider, "forgot");

        // Always return 202 regardless of whether the email exists (prevents enumeration).
        // IPasswordResetService.RequestResetAsync publishes PasswordResetRequestedEto
        // which a subscriber (Granit.Notifications or app-level) consumes to send the email.
        await passwordResetService.RequestResetAsync(request.Email, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Accepted((string?)null, (string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ResetPasswordAsync(
        AccountPasswordResetRequest request,
        HttpContext httpContext,
        [FromServices] IPasswordResetService passwordResetService,
        CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityLocalActivitySource.Source.StartActivity(
            IdentityLocalActivitySource.PasswordReset);
        activity?.SetTag(IdentityLocalActivitySource.TagProvider, "reset-token");

        try
        {
            await passwordResetService.ResetPasswordAsync(
                request.UserId, request.Token, request.NewPassword, cancellationToken).ConfigureAwait(false);

            if (Guid.TryParse(request.UserId, out Guid userId))
            {
                await PublishPasswordChangedAsync(httpContext, userId, null, cancellationToken)
                    .ConfigureAwait(false);
            }

            return TypedResults.NoContent();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Problem(
                detail: "Invalid or expired reset token.",
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task PublishPasswordChangedAsync(
        HttpContext httpContext,
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        IDistributedEventBus? eventBus = httpContext.RequestServices.GetService<IDistributedEventBus>();
        if (eventBus is null)
        {
            return;
        }

        await eventBus.PublishAsync(
            new PasswordChangedEto(userId, tenantId),
            cancellationToken).ConfigureAwait(false);
    }
}
