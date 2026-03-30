using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Local.Endpoints.Endpoints;

/// <summary>
/// Headless login endpoint for SPA/BFF architectures.
/// Authenticates the user via ASP.NET Core Identity and sets the Identity
/// session cookie without any HTTP redirects.
/// </summary>
internal static partial class AccountLoginEndpoints
{
    internal static RouteGroupBuilder MapAccountLoginEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/login", HandleLoginAsync)
            .WithName("AccountLogin")
            .WithSummary("Authenticates a user and sets the Identity session cookie.")
            .WithDescription(
                "Validates credentials (email or username + password) against ASP.NET Core Identity. "
                + "On success, sets the Identity authentication cookie and returns 200. "
                + "On failure, returns the specific failure reason (invalid credentials, 2FA required, "
                + "locked out, sign-in not allowed). This endpoint is designed for headless/BFF "
                + "architectures where the SPA handles the login UI and redirects back to "
                + "/connect/authorize after successful authentication.")
            .Produces<AccountLoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked)
            .AllowAnonymous()
            .RequireRateLimiting("authentication");

        group.MapPost("/login/two-factor", HandleTwoFactorLoginAsync)
            .WithName("AccountTwoFactorLogin")
            .WithSummary("Completes login with a TOTP code or recovery code.")
            .WithDescription(
                "Finalizes the two-factor authentication challenge after a successful "
                + "password-based login returned requiresTwoFactor: true. "
                + "The Identity.TwoFactorUserId cookie (set by the initial login) identifies "
                + "the user. Accepts either a 6-digit TOTP code from an authenticator app "
                + "or a single-use recovery code (when useRecoveryCode is true). "
                + "On success, sets the Identity authentication cookie and returns 200. "
                + "Returns 401 if the code is invalid or the 2FA session has expired.")
            .Produces<AccountLoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem()
            .AllowAnonymous()
            .RequireRateLimiting("authentication");

        return group;
    }

#pragma warning disable GRSEC003 // Method handles user credentials — server-side authentication
    private static async Task<Results<Ok<AccountLoginResponse>, ProblemHttpResult>> HandleLoginAsync(
        AccountLoginRequest request,
        [FromServices] SignInManager<GranitUser> signInManager,
        [FromServices] UserManager<GranitUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ILogger logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Identity.Local.Endpoints.AccountLoginEndpoints");
        IdentityLocalMetrics? metrics = httpContext.RequestServices.GetService<IdentityLocalMetrics>();

        // Resolve user by email or username
        GranitUser? user = await userManager.FindByEmailAsync(request.Login).ConfigureAwait(false)
            ?? await userManager.FindByNameAsync(request.Login).ConfigureAwait(false);

        if (user is null)
        {
            LogLoginFailed(logger, request.Login, "user_not_found");
            metrics?.RecordAuthenticationFailure(null, "invalid_credentials");

            return TypedResults.Problem(
                detail: "Invalid credentials.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager
            .PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            LogLoginSuccess(logger, user.Id.ToString());
            metrics?.RecordAuthenticationSuccess(null, "password");

            return TypedResults.Ok(new AccountLoginResponse(Succeeded: true));
        }

        if (result.RequiresTwoFactor)
        {
            LogLoginTwoFactor(logger, user.Id.ToString());
            return TypedResults.Ok(new AccountLoginResponse(
                Succeeded: false, RequiresTwoFactor: true));
        }

        if (result.IsLockedOut)
        {
            LogLoginLockedOut(logger, user.Id.ToString());
            metrics?.RecordAuthenticationFailure(null, "account_locked");

            return TypedResults.Problem(
                detail: "Account is locked out. Try again later.",
                statusCode: StatusCodes.Status423Locked);
        }

        if (result.IsNotAllowed)
        {
            LogLoginNotAllowed(logger, user.Id.ToString());
            metrics?.RecordAuthenticationFailure(null, "email_not_confirmed");

            return TypedResults.Problem(
                detail: "Sign-in is not allowed. Verify your email address.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Generic failure (wrong password)
        LogLoginFailed(logger, request.Login, "invalid_password");
        metrics?.RecordAuthenticationFailure(null, "invalid_credentials");

        return TypedResults.Problem(
            detail: "Invalid credentials.",
            statusCode: StatusCodes.Status401Unauthorized);
    }
#pragma warning restore GRSEC003

    private static async Task<Results<Ok<AccountLoginResponse>, ProblemHttpResult>> HandleTwoFactorLoginAsync(
        AccountTwoFactorLoginRequest request,
        [FromServices] SignInManager<GranitUser> signInManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ILogger logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Identity.Local.Endpoints.AccountLoginEndpoints");
        IdentityLocalMetrics? metrics = httpContext.RequestServices.GetService<IdentityLocalMetrics>();

        // Strip whitespace and dashes from the code (authenticator apps often format codes with spaces)
        string sanitizedCode = request.Code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        Microsoft.AspNetCore.Identity.SignInResult result;

        if (request.UseRecoveryCode)
        {
            result = await signInManager
                .TwoFactorRecoveryCodeSignInAsync(sanitizedCode)
                .ConfigureAwait(false);
        }
        else
        {
            result = await signInManager
                .TwoFactorAuthenticatorSignInAsync(sanitizedCode, isPersistent: false, rememberClient: false)
                .ConfigureAwait(false);
        }

        if (result.Succeeded)
        {
            LogTwoFactorSuccess(logger, request.UseRecoveryCode ? "recovery_code" : "totp");
            metrics?.RecordAuthenticationSuccess(null, request.UseRecoveryCode ? "recovery_code" : "totp");

            return TypedResults.Ok(new AccountLoginResponse(Succeeded: true));
        }

        if (result.IsLockedOut)
        {
            LogLoginLockedOut(logger, "two-factor-user");
            metrics?.RecordAuthenticationFailure(null, "account_locked");

            return TypedResults.Problem(
                detail: "Account is locked out. Try again later.",
                statusCode: StatusCodes.Status423Locked);
        }

        LogTwoFactorFailed(logger, request.UseRecoveryCode ? "invalid_recovery_code" : "invalid_totp_code");
        metrics?.RecordAuthenticationFailure(null, "invalid_token");

        return TypedResults.Problem(
            detail: "Invalid verification code.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Information, Message = "Headless login: user {UserId} authenticated successfully")]
    private static partial void LogLoginSuccess(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Headless login: user {UserId} requires two-factor authentication")]
    private static partial void LogLoginTwoFactor(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Headless login: user {UserId} is locked out")]
    private static partial void LogLoginLockedOut(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Headless login: user {UserId} sign-in not allowed")]
    private static partial void LogLoginNotAllowed(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Headless login: failed for '{Login}' — {Reason}")]
    private static partial void LogLoginFailed(ILogger logger, string login, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Headless login: two-factor completed via {Method}")]
    private static partial void LogTwoFactorSuccess(ILogger logger, string method);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Headless login: two-factor failed — {Reason}")]
    private static partial void LogTwoFactorFailed(ILogger logger, string reason);
}
