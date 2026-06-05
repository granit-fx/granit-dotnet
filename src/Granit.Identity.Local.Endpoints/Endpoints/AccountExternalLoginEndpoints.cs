using System.Diagnostics;
using System.Security.Claims;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Http.Idempotency.Attributes;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IdentityConstants = Microsoft.AspNetCore.Identity.IdentityConstants;

namespace Granit.Identity.Local.Endpoints.Endpoints;

internal static partial class AccountExternalLoginEndpoints
{
    internal static RouteGroupBuilder MapAccountExternalLoginEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/external-logins", ListExternalLoginsAsync)
            .WithName("ListExternalLogins")
            .WithSummary("Lists linked external login providers.")
            .WithDescription(
                "Returns the list of external providers (Google, Microsoft, GitHub) "
                + "currently linked to the authenticated user's account.")
            .Produces<IReadOnlyList<ExternalLoginInfoResponse>>()
            .RequireAuthorization();

        group.MapPost("/external-logins/challenge/{provider}", ChallengeAsync)
            .WithName("ChallengeExternalLogin")
            .WithSummary("Initiates an OAuth flow with an external provider.")
            .WithDescription(
                "Validates that the specified provider is registered in IExternalProviderRegistry. "
                + "Returns 200 to confirm the provider is available; the frontend then initiates "
                + "the OAuth redirect via the standard client challenge flow. "
                + "Returns 400 if the provider is not configured.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        group.MapGet("/external-logins/callback", CallbackAsync)
            .WithName("ExternalLoginCallback")
            .WithSummary("Processes the OAuth callback from an external provider.")
            .WithDescription(
                "Handles the redirect from the external provider. Links the external account "
                + "to an existing user or creates a new one if AutoRegisterExternalUsers is enabled. "
                + "Returns 400 if the provider query parameter is missing. "
                + "Returns 409 if the email is taken by another account. "
                + "Returns 403 if auto-registration is disabled and no account exists.")
            .Produces<ProcessCallbackResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous();

        group.MapDelete("/external-logins/{provider}", UnlinkExternalLoginAsync)
            .WithName("UnlinkExternalLogin")
            .WithSummary("Unlinks an external login provider.")
            .WithDescription(
                "Removes the association between the authenticated user and the specified provider. "
                + "Returns 400 if it's the last login method and no password is set.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<ExternalLoginInfoResponse>>> ListExternalLoginsAsync(
        HttpContext httpContext,
        [FromServices] IExternalLoginService externalLoginService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        IReadOnlyList<ExternalLoginInfo> logins = await externalLoginService
            .GetLoginsAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok<IReadOnlyList<ExternalLoginInfoResponse>>(
            logins.Select(IdentityLocalResponseMapper.ToResponse).ToList());
    }

    private static Task<Results<Ok, ProblemHttpResult>> ChallengeAsync(
        string provider,
        [FromServices] IExternalProviderRegistry providerRegistry)
    {
        // Validate provider is configured
        bool isConfigured = providerRegistry.IsProviderConfigured(provider);

        if (!isConfigured)
        {
            return Task.FromResult<Results<Ok, ProblemHttpResult>>(
                TypedResults.Problem(
                    detail: "The specified external login provider is not configured.",
                    statusCode: StatusCodes.Status400BadRequest));
        }

        // The actual OAuth challenge is initiated by the auth server's client middleware.
        // The host application configures challenge properties and calls ChallengeAsync()
        // on the authentication scheme corresponding to the provider.
        // This endpoint validates the provider and returns metadata for the frontend
        // to initiate the redirect via the standard OAuth client flow.
        return Task.FromResult<Results<Ok, ProblemHttpResult>>(TypedResults.Ok());
    }

    private static async Task<Results<Ok<ProcessCallbackResult>, ProblemHttpResult>> CallbackAsync(
        HttpContext httpContext,
        [FromServices] IExternalLoginService externalLoginService,
        CancellationToken cancellationToken)
    {
        // Resolve the provider from the external auth ticket — never from the query
        // string. The query string is attacker-controlled: a malicious caller could
        // target the Google OAuth callback URL with ?provider=GitHub and cause the
        // link table to mis-attribute the resulting external login. ASP.NET Core
        // Identity stores the originating scheme in
        // AuthenticationProperties.Items["LoginProvider"] under the well-known
        // IdentityConstants.ExternalScheme cookie.
        ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Identity.Local.Endpoints.AccountExternalLoginEndpoints");

        AuthenticateResult authResult = await httpContext
            .AuthenticateAsync(IdentityConstants.ExternalScheme)
            .ConfigureAwait(false);

        if (!authResult.Succeeded
            || authResult.Principal is null
            || authResult.Properties is null
            || !authResult.Properties.Items.TryGetValue("LoginProvider", out string? provider)
            || string.IsNullOrEmpty(provider))
        {
            await TryWriteExternalLoginAuditAsync(httpContext, logger, provider: "unknown",
                userId: null, userName: null, failureReason: "callback_missing_scheme",
                cancellationToken).ConfigureAwait(false);
            return TypedResults.Problem(
                detail: "External login callback did not carry an authentication scheme.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        string? externalUserId = authResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        string? externalUserName = authResult.Principal.Identity?.Name;

        try
        {
            ProcessCallbackResult result = await externalLoginService
                .ProcessCallbackAsync(authResult.Principal, provider, cancellationToken)
                .ConfigureAwait(false);
            await TryWriteExternalLoginAuditAsync(httpContext, logger, provider,
                userId: externalUserId, userName: externalUserName, failureReason: null,
                cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            await TryWriteExternalLoginAuditAsync(httpContext, logger, provider,
                userId: externalUserId, userName: externalUserName,
                failureReason: "account_not_found", cancellationToken).ConfigureAwait(false);
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("DuplicateEmail", StringComparison.OrdinalIgnoreCase))
        {
            await TryWriteExternalLoginAuditAsync(httpContext, logger, provider,
                userId: externalUserId, userName: externalUserName,
                failureReason: "duplicate_email", cancellationToken).ConfigureAwait(false);
            return TypedResults.Problem(
                detail: "An account with this email already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    /// <summary>
    /// Records an external-login callback audit row. No-op when
    /// <see cref="IAuditingWriter"/> is not registered; audit failures are
    /// swallowed so the login flow is never blocked by audit issues.
    /// </summary>
    private static async Task TryWriteExternalLoginAuditAsync(
        HttpContext httpContext,
        ILogger logger,
        string provider,
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
        string method = $"external:{provider.ToLowerInvariant()}";

        AuditEntry entry = failureReason is null
            ? AuthenticationAuditEntry.CreateSuccess(
                timeProvider.GetUtcNow(),
                userId: string.IsNullOrEmpty(userId) ? AuthenticationAuditEntry.UnknownUserSentinel : userId,
                userName, method, tenantId, ipAddress, userAgent, correlationId)
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

    [LoggerMessage(Level = LogLevel.Error, Message = "External login: failed to write authentication audit entry — login flow continues.")]
    private static partial void LogAuditWriteFailed(ILogger logger, Exception exception);

    private static async Task<Results<NoContent, ProblemHttpResult>> UnlinkExternalLoginAsync(
        string provider,
        HttpContext httpContext,
        [FromServices] IExternalLoginService externalLoginService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;

        // Get the provider key for this provider
        IReadOnlyList<ExternalLoginInfo> logins = await externalLoginService
            .GetLoginsAsync(userId, cancellationToken).ConfigureAwait(false);

        ExternalLoginInfo? login = logins.FirstOrDefault(l =>
            l.LoginProvider.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (login is null)
        {
            return TypedResults.Problem(
                detail: $"No linked login found for provider '{provider}'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await externalLoginService
                .RemoveLoginAsync(userId, login.LoginProvider, login.ProviderKey, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
