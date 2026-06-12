using System.Diagnostics;
using System.Security.Cryptography;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Events;
using Granit.Http.Timing;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
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
    private const string LocalLoginMethod = "password";
    private const string InvalidLoginReason = "invalid_credentials";
    private const string AccountLockedReason = "account_locked";

    internal static RouteGroupBuilder MapAccountLoginEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/login", HandleLoginAsync)
            .WithName("AccountLogin")
            .WithSummary("Authenticates a user and sets the Identity session cookie.")
            .WithDescription(
                "Validates credentials (email or username + password) against ASP.NET Core Identity. "
                + "On success, sets the Identity authentication cookie and returns 200. "
                + "On failure, returns 401 with a generic message to prevent account enumeration. "
                + "Locked-out users are notified exclusively via email with a password reset link. "
                + "This endpoint is designed for headless/BFF architectures where the SPA handles "
                + "the login UI and redirects back to /connect/authorize after successful authentication.")
            .Produces<AccountLoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .AllowAnonymous()
            .RequireRateLimiting("authentication");

        group.MapPost("/login/two-factor", HandleTwoFactorLoginAsync)
            .WithName("AccountTwoFactorLogin")
            .WithSummary("Completes login with a TOTP, email, or recovery code.")
            .WithDescription(
                "Finalizes the two-factor authentication challenge after a successful "
                + "password-based login returned requiresTwoFactor: true. "
                + "The Identity.TwoFactorUserId cookie (set by the initial login) identifies "
                + "the user. The method field selects the factor: a 6-digit TOTP code from an "
                + "authenticator app, a one-time code sent by email, or a single-use recovery code. "
                + "On success, sets the Identity authentication cookie and returns 200. "
                + "Returns 401 if the code is invalid or the 2FA session has expired.")
            .Produces<AccountLoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem()
            .AllowAnonymous()
            .RequireRateLimiting("authentication");

        group.MapPost("/login/two-factor/send-email", SendTwoFactorEmailCodeAsync)
            .WithName("SendTwoFactorLoginEmailCode")
            .WithSummary("Sends a one-time code by email for the two-factor challenge.")
            .WithDescription(
                "Generates and emails a one-time code to the user identified by the "
                + "Identity.TwoFactorUserId cookie, for use with the email two-factor method. "
                + "Only sends when the user has enrolled the email factor. Always returns 204 "
                + "(even when no code is sent) to avoid leaking which factors are configured. "
                + "Returns 400 if there is no active two-factor session.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous()
            .RequireRateLimiting("authentication");

        return group;
    }

#pragma warning disable GRSEC003 // Method handles user credentials — server-side authentication
    private static async Task<Results<Ok<AccountLoginResponse>, ProblemHttpResult>> HandleLoginAsync(
        AccountLoginRequest request,
        [FromServices] SignInManager<LocalIdentity> signInManager,
        [FromServices] UserManager<LocalIdentity> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Enforce a minimum response time to prevent timing side-channels.
        // Without this, an attacker can distinguish "user not found" (~310ms with
        // dummy hash) from "wrong password on existing account" (~450ms with hash
        // + DB writes for lockout counter). The floor is randomized per request
        // (500-700ms) so an attacker cannot fingerprint a fixed threshold.
        await using var floor = MinimumResponseTimeGuard.Begin(
            MinResponseFloorMs, MaxResponseFloorMs);

        using Activity? activity = IdentityLocalActivitySource.Source.StartActivity(
            IdentityLocalActivitySource.UserAuthentication);
        activity?.SetTag(IdentityLocalActivitySource.TagProvider, LocalLoginMethod);

        return await HandleLoginCoreAsync(
            request, signInManager, userManager, httpContext, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<Results<Ok<AccountLoginResponse>, ProblemHttpResult>> HandleLoginCoreAsync(
        AccountLoginRequest request,
        SignInManager<LocalIdentity> signInManager,
        UserManager<LocalIdentity> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ILogger logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Identity.Local.Endpoints.AccountLoginEndpoints");
        IdentityLocalMetrics? metrics = httpContext.RequestServices.GetService<IdentityLocalMetrics>();
        ICurrentTenant? currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
        IDataFilter? dataFilter = httpContext.RequestServices.GetService<IDataFilter>();

        // Resolve user by email or username.
        // Always disable the multi-tenant filter for lookup: when the caller already
        // has an Identity cookie from a prior login on another side (common on
        // localhost where cookies ignore the port), ICurrentTenant resolves to the
        // stale claim and a tenant-scoped query would hide any user whose TenantId
        // does not match — making it impossible to log in as a host admin from a
        // browser that previously authenticated a tenant user, and vice-versa.
        // RequireUniqueEmail=true guarantees no ambiguity across tenants.
        LocalIdentity? user;
        IDisposable? tenantFilterScope = dataFilter?.Disable<IMultiTenant>();
        try
        {
            user = await userManager.FindByEmailAsync(request.Login).ConfigureAwait(false)
                ?? await userManager.FindByNameAsync(request.Login).ConfigureAwait(false);
        }
        finally
        {
            tenantFilterScope?.Dispose();
        }

        if (user is null)
        {
            // Perform a dummy hash so BCrypt/Argon2 CPU cost is incurred even
            // when the user does not exist (covers the bulk of the timing gap).
            PerformDummyPasswordHash(httpContext);

            LogLoginFailed(logger, request.Login, "user_not_found");
            metrics?.RecordAuthenticationFailure(null, InvalidLoginReason);
            await TryWriteAuthAuditAsync(httpContext, logger,
                method: LocalLoginMethod, userId: null, userName: null,
                failureReason: InvalidLoginReason, cancellationToken).ConfigureAwait(false);

            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Account:InvalidCredentials", "Invalid credentials."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Re-align the tenant context with the resolved user so PasswordSignInAsync
        // and downstream Identity operations run within the correct scope. Passing
        // `user.TenantId` handles both cases: a host admin (null) clears any stale
        // tenant inherited from a prior login, and a tenant user switches the
        // context to their own tenant even if it differs from the stale claim.
        using IDisposable? tenantScope = currentTenant?.Change(user.TenantId);

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager
            .PasswordSignInAsync(user, request.Password, isPersistent: request.RememberMe, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            LogLoginSuccess(logger, user.Id.ToString());
            metrics?.RecordAuthenticationSuccess(null, LocalLoginMethod);
            await TryWriteAuthAuditAsync(httpContext, logger,
                method: LocalLoginMethod, userId: user.Id.ToString(), userName: user.UserName,
                failureReason: null, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(new AccountLoginResponse(Succeeded: true));
        }

        if (result.RequiresTwoFactor)
        {
            LogLoginTwoFactor(logger, user.Id.ToString());

            // Surface the factors the user can complete the challenge with so the SPA
            // knows whether to offer the "email me a code" action alongside the
            // authenticator/recovery inputs.
            ITwoFactorService twoFactorService = httpContext.RequestServices
                .GetRequiredService<ITwoFactorService>();
            IReadOnlyList<TwoFactorMethod> methods = await twoFactorService
                .GetAvailableMethodsAsync(user.Id.ToString(), cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(new AccountLoginResponse(
                Succeeded: false, RequiresTwoFactor: true,
                TwoFactorMethods: [.. methods.Select(static m => m.ToString())]));
        }

        if (result.IsLockedOut)
        {
            LogLoginLockedOut(logger, user.Id.ToString());
            metrics?.RecordAuthenticationFailure(null, AccountLockedReason);

            await PublishAccountLockedAsync(httpContext, userManager, user, cancellationToken)
                .ConfigureAwait(false);
            await TryWriteAuthAuditAsync(httpContext, logger,
                method: LocalLoginMethod, userId: user.Id.ToString(), userName: user.UserName,
                failureReason: AccountLockedReason, cancellationToken).ConfigureAwait(false);

            // Return 401 (same as invalid credentials) to prevent account enumeration.
            // The user is notified of the lockout exclusively via email.
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Account:InvalidCredentials", "Invalid credentials."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (result.IsNotAllowed)
        {
            LogLoginNotAllowed(logger, user.Id.ToString());
            metrics?.RecordAuthenticationFailure(null, "email_not_confirmed");
            await TryWriteAuthAuditAsync(httpContext, logger,
                method: LocalLoginMethod, userId: user.Id.ToString(), userName: user.UserName,
                failureReason: "email_not_confirmed", cancellationToken).ConfigureAwait(false);

            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Account:SignInNotAllowed", "Sign-in is not allowed. Verify your email address."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Generic failure (wrong password)
        LogLoginFailed(logger, request.Login, "invalid_password");
        metrics?.RecordAuthenticationFailure(null, InvalidLoginReason);
        await TryWriteAuthAuditAsync(httpContext, logger,
            method: LocalLoginMethod, userId: user.Id.ToString(), userName: user.UserName,
            failureReason: InvalidLoginReason, cancellationToken).ConfigureAwait(false);

        return TypedResults.Problem(
            detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Account:InvalidCredentials", "Invalid credentials."),
            statusCode: StatusCodes.Status401Unauthorized);
    }
#pragma warning restore GRSEC003

    private static async Task<Results<Ok<AccountLoginResponse>, ProblemHttpResult>> HandleTwoFactorLoginAsync(
        AccountTwoFactorLoginRequest request,
        [FromServices] SignInManager<LocalIdentity> signInManager,
        [FromServices] IEmailTwoFactorService emailTwoFactorService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string method = request.Method switch
        {
            TwoFactorMethod.RecoveryCode => "recovery_code",
            TwoFactorMethod.Email => "email_otp",
            _ => "totp",
        };
        string failureReason = request.Method switch
        {
            TwoFactorMethod.RecoveryCode => "invalid_recovery_code",
            TwoFactorMethod.Email => "invalid_email_otp",
            _ => "invalid_totp_code",
        };

        using Activity? activity = IdentityLocalActivitySource.Source.StartActivity(
            IdentityLocalActivitySource.TwoFactorChallenge);
        activity?.SetTag(IdentityLocalActivitySource.TagProvider, method);

        ILogger logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Identity.Local.Endpoints.AccountLoginEndpoints");
        IdentityLocalMetrics? metrics = httpContext.RequestServices.GetService<IdentityLocalMetrics>();
        ICurrentTenant? currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
        IDataFilter? dataFilter = httpContext.RequestServices.GetService<IDataFilter>();

        // The 2FA session stores the user's ID from the first login step (Identity.TwoFactorUserId cookie).
        // GetTwoFactorAuthenticationUserAsync internally calls FindByIdAsync which is subject to
        // the multi-tenant query filter. When no tenant is resolved, disable the filter to find
        // the user, then establish the tenant context for the rest of the handler.
        using IDisposable? tenantScope = await ResolveTenantFromTwoFactorSessionAsync(
            signInManager, currentTenant, dataFilter).ConfigureAwait(false);

        // Strip whitespace and dashes from the code (authenticator apps often format codes with spaces)
        string sanitizedCode = request.Code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        Microsoft.AspNetCore.Identity.SignInResult result = await PerformTwoFactorSignInAsync(
            request, signInManager, emailTwoFactorService, httpContext, sanitizedCode, cancellationToken)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            LogTwoFactorSuccess(logger, method);
            metrics?.RecordAuthenticationSuccess(null, method);
            LocalIdentity? signedInUser = await signInManager.UserManager
                .GetUserAsync(httpContext.User).ConfigureAwait(false);
            await TryWriteAuthAuditAsync(httpContext, logger,
                method: method,
                userId: signedInUser?.Id.ToString(),
                userName: signedInUser?.UserName,
                failureReason: null,
                cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(new AccountLoginResponse(Succeeded: true));
        }

        if (result.IsLockedOut)
        {
            LocalIdentity? lockedUser = await signInManager.GetTwoFactorAuthenticationUserAsync()
                .ConfigureAwait(false);

            LogLoginLockedOut(logger, lockedUser?.Id.ToString() ?? "two-factor-user");
            metrics?.RecordAuthenticationFailure(null, AccountLockedReason);

            if (lockedUser is not null)
            {
                await PublishAccountLockedAsync(
                    httpContext, signInManager.UserManager, lockedUser, cancellationToken)
                    .ConfigureAwait(false);
            }

            await TryWriteAuthAuditAsync(httpContext, logger,
                method: method,
                userId: lockedUser?.Id.ToString(),
                userName: lockedUser?.UserName,
                failureReason: AccountLockedReason,
                cancellationToken).ConfigureAwait(false);

            // Return 401 (same as invalid code) to prevent account enumeration.
            // The user is notified of the lockout exclusively via email.
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:TwoFactor:InvalidCode", "Invalid verification code."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        LogTwoFactorFailed(logger, failureReason);
        metrics?.RecordAuthenticationFailure(null, "invalid_token");
        LocalIdentity? twoFactorUser = await signInManager.GetTwoFactorAuthenticationUserAsync()
            .ConfigureAwait(false);
        await TryWriteAuthAuditAsync(httpContext, logger,
            method: method,
            userId: twoFactorUser?.Id.ToString(),
            userName: twoFactorUser?.UserName,
            failureReason: failureReason,
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Problem(
            detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:TwoFactor:InvalidCode", "Invalid verification code."),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    // Performs the second-factor sign-in for the requested method, returning the raw
    // SignInResult for the caller to translate into success / lockout / failure responses.
    private static async Task<Microsoft.AspNetCore.Identity.SignInResult> PerformTwoFactorSignInAsync(
        AccountTwoFactorLoginRequest request,
        SignInManager<LocalIdentity> signInManager,
        IEmailTwoFactorService emailTwoFactorService,
        HttpContext httpContext,
        string sanitizedCode,
        CancellationToken cancellationToken)
    {
        switch (request.Method)
        {
            case TwoFactorMethod.RecoveryCode:
                Microsoft.AspNetCore.Identity.SignInResult recoveryResult = await signInManager
                    .TwoFactorRecoveryCodeSignInAsync(sanitizedCode)
                    .ConfigureAwait(false);

                // TwoFactorRecoveryCodeSignInAsync does not accept isPersistent,
                // so re-sign the user with a persistent cookie when RememberMe is requested.
                if (recoveryResult.Succeeded && request.RememberMe)
                {
                    LocalIdentity? user = await signInManager.UserManager
                        .GetUserAsync(httpContext.User).ConfigureAwait(false);

                    if (user is not null)
                    {
                        await signInManager.SignInAsync(user, isPersistent: true)
                            .ConfigureAwait(false);
                    }
                }

                return recoveryResult;

            case TwoFactorMethod.Email:
                // The ASP.NET email token provider validates a code for ANY user with a
                // confirmed email, so gate the method on explicit enrollment — otherwise
                // email access alone would bypass a configured authenticator.
                LocalIdentity? emailUser = await signInManager
                    .GetTwoFactorAuthenticationUserAsync().ConfigureAwait(false);

                return emailUser is not null
                    && await emailTwoFactorService
                        .IsEnabledAsync(emailUser.Id.ToString(), cancellationToken).ConfigureAwait(false)
                    ? await signInManager.TwoFactorSignInAsync(
                        TokenOptions.DefaultEmailProvider, sanitizedCode,
                        isPersistent: request.RememberMe, rememberClient: false).ConfigureAwait(false)
                    : Microsoft.AspNetCore.Identity.SignInResult.Failed;

            default:
                return await signInManager
                    .TwoFactorAuthenticatorSignInAsync(sanitizedCode, isPersistent: request.RememberMe, rememberClient: false)
                    .ConfigureAwait(false);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> SendTwoFactorEmailCodeAsync(
        [FromServices] SignInManager<LocalIdentity> signInManager,
        [FromServices] IEmailTwoFactorService emailTwoFactorService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ICurrentTenant? currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
        IDataFilter? dataFilter = httpContext.RequestServices.GetService<IDataFilter>();

        using IDisposable? tenantScope = await ResolveTenantFromTwoFactorSessionAsync(
            signInManager, currentTenant, dataFilter).ConfigureAwait(false);

        LocalIdentity? user = await signInManager.GetTwoFactorAuthenticationUserAsync()
            .ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:TwoFactor:NoActiveSession", "No active two-factor session."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Send only when the email factor is actually enrolled — prevents unsolicited mail
        // and an authenticator bypass via the always-available email token provider. The
        // response is 204 regardless, so a caller cannot probe which factors are configured.
        if (await emailTwoFactorService.IsEnabledAsync(user.Id.ToString(), cancellationToken)
            .ConfigureAwait(false))
        {
            await emailTwoFactorService.SendCodeAsync(user.Id.ToString(), cancellationToken)
                .ConfigureAwait(false);
        }

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Resolves the tenant context from the 2FA session cookie and aligns
    /// <see cref="ICurrentTenant"/> with the user identified by the challenge.
    /// Returns an <see cref="IDisposable"/> that restores the previous tenant on
    /// dispose, or <c>null</c> when the 2FA user cannot be resolved (the handler
    /// lets the subsequent sign-in call surface the failure).
    /// </summary>
    /// <remarks>
    /// The filter is disabled unconditionally during lookup: a stale tenant claim
    /// inherited from a prior login on another side would otherwise scope the
    /// query to the wrong tenant and hide the 2FA user, mirroring the password
    /// login issue. The scope returned by <see cref="ICurrentTenant.Change"/> is
    /// entered with the user's actual <c>TenantId</c> (null for host admins) so
    /// the rest of the 2FA ceremony runs in the correct context.
    /// </remarks>
    private static async Task<IDisposable?> ResolveTenantFromTwoFactorSessionAsync(
        SignInManager<LocalIdentity> signInManager,
        ICurrentTenant? currentTenant,
        IDataFilter? dataFilter)
    {
        if (currentTenant is null)
        {
            return null;
        }

        using IDisposable? filterScope = dataFilter?.Disable<IMultiTenant>();

        LocalIdentity? twoFactorUser = await signInManager.GetTwoFactorAuthenticationUserAsync()
            .ConfigureAwait(false);

        return twoFactorUser is null ? null : currentTenant.Change(twoFactorUser.TenantId);
    }

    // ──── Timing attack mitigation ────

    /// <summary>
    /// Lower bound of the per-request response-time floor used by the login endpoints.
    /// Shared with <c>AccountPasswordEndpoints.ForgotPasswordAsync</c> so credential
    /// recovery and login present the same timing surface.
    /// </summary>
    internal const int MinResponseFloorMs = 500;

    /// <summary>
    /// Upper bound of the per-request response-time floor.
    /// </summary>
    internal const int MaxResponseFloorMs = 700;

    /// <summary>
    /// Pre-computed BCrypt/Argon2 hash used for dummy verification when the user
    /// does not exist. The actual password value is irrelevant — what matters is
    /// that VerifyHashedPassword runs the same hashing algorithm as a real check,
    /// burning ~300ms of CPU so the response time is indistinguishable from a
    /// real password verification.
    /// </summary>
    private static readonly string DummyPasswordHash =
        new PasswordHasher<LocalIdentity>().HashPassword(null!, "K4$hDummyP@ssw0rd!");

    private static void PerformDummyPasswordHash(HttpContext httpContext)
    {
        IPasswordHasher<LocalIdentity> hasher = httpContext.RequestServices
            .GetRequiredService<IPasswordHasher<LocalIdentity>>();

        // Use a random password each time to introduce natural timing jitter
        // and avoid a constant, fingerprint-able verification pattern.
        hasher.VerifyHashedPassword(null!, DummyPasswordHash, RandomNumberGenerator.GetHexString(32));
    }

    // ──── Lockout event publishing ────

    private static async Task PublishAccountLockedAsync(
        HttpContext httpContext,
        UserManager<LocalIdentity> userManager,
        LocalIdentity user,
        CancellationToken cancellationToken)
    {
        IDistributedEventBus? eventBus = httpContext.RequestServices.GetService<IDistributedEventBus>();
        if (eventBus is null || user.Email is null)
        {
            return;
        }

        string resetToken = await userManager.GeneratePasswordResetTokenAsync(user)
            .ConfigureAwait(false);

        int maxAttempts = userManager.Options.Lockout.MaxFailedAccessAttempts;
        // LockoutEnd is always set when IsLockedOut is true; fallback is a safety net.
        DateTimeOffset lockoutEnd = user.LockoutEnd
            ?? httpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();

        await eventBus.PublishAsync(
            new AccountLockedEto(user.Id, user.Email, maxAttempts, resetToken, lockoutEnd, user.TenantId),
            cancellationToken).ConfigureAwait(false);
    }

    // ──── Authentication audit ────

    /// <summary>
    /// Records an authentication audit row for the local-identity login flow.
    /// No-op when <see cref="IAuditingWriter"/> is not registered. Audit
    /// failures are swallowed so a transient audit-store outage never breaks
    /// authentication.
    /// </summary>
    private static async Task TryWriteAuthAuditAsync(
        HttpContext httpContext,
        ILogger logger,
        string method,
        string? userId,
        string? userName,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        IAuditingWriter? auditingWriter = httpContext.RequestServices.GetService<IAuditingWriter>();
        if (auditingWriter is null)
        {
            return;
        }

        TimeProvider timeProvider = httpContext.RequestServices.GetService<TimeProvider>()
            ?? TimeProvider.System;
        ICurrentTenant? currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
        Guid? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null;
        string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        string? userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }
        string? correlationId = Activity.Current?.Id;

        AuditEntry entry = failureReason is null
            ? AuthenticationAuditEntry.CreateSuccess(
                timeProvider.GetUtcNow(), userId!, userName, method,
                tenantId, ipAddress, userAgent, correlationId)
            : AuthenticationAuditEntry.CreateFailure(
                timeProvider.GetUtcNow(), userId, userName, method, failureReason,
                tenantId, ipAddress, userAgent, correlationId);

        try
        {
            await auditingWriter.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuditWriteFailed(logger, ex);
        }
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Headless login: failed to write authentication audit entry — login flow continues.")]
    private static partial void LogAuditWriteFailed(ILogger logger, Exception exception);
}
