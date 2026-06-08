using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Claims;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Endpoints.Internal;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Granit.OpenIddict.Endpoints.Endpoints;

/// <summary>
/// OIDC token endpoint (<c>POST /connect/token</c>).
/// Handles authorization_code, client_credentials, and refresh_token grant types.
/// </summary>
#pragma warning disable GRAPI001 // OpenIddict requires Results.SignIn/Forbid with explicit auth scheme
#pragma warning disable GRAPI003 // Private handler methods — not exposed as endpoint parameters
internal static partial class ConnectTokenEndpoints
{
    internal static IEndpointRouteBuilder MapConnectTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/token", (Delegate)HandleTokenAsync)
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<IResult> HandleTokenAsync(HttpContext context)
    {
        OpenIddictRequest request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict server request is not available.");

        OidcPrincipalFactory principalFactory = context.RequestServices
            .GetRequiredService<OidcPrincipalFactory>();
        OpenIddictMetrics metrics = context.RequestServices
            .GetRequiredService<OpenIddictMetrics>();
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.OpenIddict.Endpoints.ConnectTokenEndpoints");
        ICurrentTenant? currentTenant = context.RequestServices
            .GetService<ICurrentTenant>();
        string? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id?.ToString() : null;

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            return await HandleCodeOrRefreshAsync(
                context, request, principalFactory, metrics, tenantId)
                .ConfigureAwait(false);
        }

        if (request.IsClientCredentialsGrantType())
        {
            return HandleClientCredentials(context, request, metrics, tenantId);
        }

        if (request.GrantType == "urn:granit:grant_type:two_factor")
        {
            return await HandleTwoFactorAsync(
                context, request, principalFactory, metrics, tenantId)
                .ConfigureAwait(false);
        }

        if (request.GrantType == "urn:granit:grant_type:passkey")
        {
            return await HandlePasskeyAsync(
                context, request, principalFactory, metrics, tenantId)
                .ConfigureAwait(false);
        }

        LogUnsupportedGrantType(logger, request.GrantType ?? "(null)");
        return Results.Forbid(
            authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> HandleCodeOrRefreshAsync(
        HttpContext context,
        OpenIddictRequest request,
        OidcPrincipalFactory principalFactory,
        OpenIddictMetrics metrics,
        string? tenantId)
    {
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.OpenIddict.Endpoints.ConnectTokenEndpoints");

        // OpenIddict has already validated the code/refresh_token — authenticate to get the principal.
        AuthenticateResult authenticateResult = await context.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);

        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            LogAuthenticationFailed(logger, request.GrantType!);
            metrics.RecordAuthenticationFailure(tenantId, "invalid_token");
            await TryWriteAuthAuditAsync(context, logger,
                method: request.GrantType!, userId: null, userName: null,
                failureReason: "invalid_token", tenantId).ConfigureAwait(false);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Restore tenant context from the token's tenant_id claim before looking up the user.
        // For refresh_token grants, no tenant middleware has run — the claim is the only source.
        // When absent (host admin), the filter naturally matches TenantId IS NULL.
        string? subject = authenticateResult.Principal.GetClaim(OpenIddictConstants.Claims.Subject);
        string? tokenTenantId = authenticateResult.Principal.GetClaim("tenant_id");
        ICurrentTenant? currentTenant = context.RequestServices.GetService<ICurrentTenant>();
        IDisposable? tenantScope = null;
        if (tokenTenantId is not null && Guid.TryParse(tokenTenantId, out Guid tenantGuid))
        {
            tenantScope = currentTenant?.Change(tenantGuid);
            tenantId ??= tokenTenantId;
        }

        try
        {
            // Retrieve the user from the subject claim.
            UserManager<LocalIdentity> userManager = context.RequestServices
                .GetRequiredService<UserManager<LocalIdentity>>();

            LocalIdentity? user = subject is not null
                ? await userManager.FindByIdAsync(subject).ConfigureAwait(false)
                : null;

            if (user is null)
            {
                LogUserNotFound(logger, subject ?? "(null)");
                metrics.RecordAuthenticationFailure(tenantId, "invalid_credentials");
                await TryWriteAuthAuditAsync(context, logger,
                    method: request.GrantType!, userId: subject, userName: null,
                    failureReason: "invalid_credentials", tenantId).ConfigureAwait(false);
                return Results.Forbid(
                    authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            // Rebuild the principal with up-to-date claims.
            // Preserve scopes from the original principal (including offline_access for refresh tokens).
            ImmutableArray<string> scopes = authenticateResult.Principal.GetScopes();

            ClaimsPrincipal principal = await principalFactory.CreateUserPrincipalAsync(
                user, scopes, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
                .ConfigureAwait(false);

            string grantType = request.GrantType!;
            metrics.RecordTokenIssued(tenantId, grantType);
            metrics.RecordAuthenticationSuccess(tenantId, grantType);
            LogTokenIssued(logger, user.Id.ToString(), grantType);

            await TryWriteAuthAuditAsync(context, logger,
                method: grantType,
                userId: user.Id.ToString(),
                userName: user.UserName,
                failureReason: null,
                tenantId).ConfigureAwait(false);

            return Results.SignIn(principal,
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        finally
        {
            tenantScope?.Dispose();
        }
    }

    private static IResult HandleClientCredentials(
        HttpContext context,
        OpenIddictRequest request,
        OpenIddictMetrics metrics,
        string? tenantId)
    {
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.OpenIddict.Endpoints.ConnectTokenEndpoints");

        ClaimsPrincipal principal = OidcPrincipalFactory.CreateClientPrincipal(
            request.ClientId!,
            request.GetScopes(),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        metrics.RecordTokenIssued(tenantId, "client_credentials");
        LogTokenIssued(logger, request.ClientId!, "client_credentials");

        return Results.SignIn(principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> HandleTwoFactorAsync(
        HttpContext context,
        OpenIddictRequest request,
        OidcPrincipalFactory principalFactory,
        OpenIddictMetrics metrics,
        string? tenantId)
    {
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.OpenIddict.Endpoints.ConnectTokenEndpoints");

        UserManager<LocalIdentity> userManager = context.RequestServices
            .GetRequiredService<UserManager<LocalIdentity>>();

        string? username = (string?)request["username"];
        string? code = (string?)request["code"];
        bool useRecoveryCode = string.Equals(
            (string?)request["use_recovery_code"], "true",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(code))
        {
            LogTwoFactorMissingParams(logger);
            metrics.RecordAuthenticationFailure(tenantId, "missing_params");
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        LocalIdentity? user = await userManager.FindByNameAsync(username).ConfigureAwait(false);
        if (user is null)
        {
            LogUserNotFound(logger, username);
            metrics.RecordAuthenticationFailure(tenantId, "invalid_credentials");
            await TryWriteAuthAuditAsync(context, logger,
                method: "urn:granit:grant_type:two_factor", userId: null, userName: username,
                failureReason: "invalid_credentials", tenantId).ConfigureAwait(false);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        bool valid;
        if (useRecoveryCode)
        {
            IdentityResult result = await userManager
                .RedeemTwoFactorRecoveryCodeAsync(user, code).ConfigureAwait(false);
            valid = result.Succeeded;
        }
        else
        {
            valid = await userManager
                .VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, code)
                .ConfigureAwait(false);
        }

        if (!valid)
        {
            LogTwoFactorFailed(logger, user.Id.ToString());
            metrics.RecordAuthenticationFailure(tenantId, "invalid_two_factor_code");
            await TryWriteAuthAuditAsync(context, logger,
                method: "urn:granit:grant_type:two_factor", userId: user.Id.ToString(), userName: user.UserName,
                failureReason: "invalid_two_factor_code", tenantId).ConfigureAwait(false);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        ImmutableArray<string> scopes = request.GetScopes();
        ClaimsPrincipal principal = await principalFactory.CreateUserPrincipalAsync(
            user, scopes, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .ConfigureAwait(false);

        metrics.RecordTokenIssued(tenantId, "urn:granit:grant_type:two_factor");
        metrics.RecordAuthenticationSuccess(tenantId, "urn:granit:grant_type:two_factor");
        LogTokenIssued(logger, user.Id.ToString(), "urn:granit:grant_type:two_factor");

        await TryWriteAuthAuditAsync(context, logger,
            method: "urn:granit:grant_type:two_factor",
            userId: user.Id.ToString(),
            userName: user.UserName,
            failureReason: null,
            tenantId).ConfigureAwait(false);

        return Results.SignIn(principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> HandlePasskeyAsync(
        HttpContext context,
        OpenIddictRequest request,
        OidcPrincipalFactory principalFactory,
        OpenIddictMetrics metrics,
        string? tenantId)
    {
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.OpenIddict.Endpoints.ConnectTokenEndpoints");

        string? credentialJson = (string?)request["credential_json"];
        if (string.IsNullOrEmpty(credentialJson))
        {
            LogPasskeyMissingCredential(logger);
            metrics.RecordAuthenticationFailure(tenantId, "missing_params");
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        IPasskeyService passkeyService = context.RequestServices
            .GetRequiredService<IPasskeyService>();

        GranitPasskeyAssertionResult assertion = await passkeyService
            .CompleteAssertionAsync(credentialJson, context.RequestAborted)
            .ConfigureAwait(false);

        if (!assertion.Succeeded || assertion.UserId is null)
        {
            LogPasskeyAssertionFailed(logger);
            metrics.RecordAuthenticationFailure(tenantId, "invalid_passkey");
            await TryWriteAuthAuditAsync(context, logger,
                method: "urn:granit:grant_type:passkey", userId: null, userName: null,
                failureReason: "invalid_passkey", tenantId).ConfigureAwait(false);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        UserManager<LocalIdentity> userManager = context.RequestServices
            .GetRequiredService<UserManager<LocalIdentity>>();

        LocalIdentity? user = await userManager.FindByIdAsync(assertion.UserId).ConfigureAwait(false);
        if (user is null)
        {
            LogUserNotFound(logger, assertion.UserId);
            metrics.RecordAuthenticationFailure(tenantId, "invalid_credentials");
            await TryWriteAuthAuditAsync(context, logger,
                method: "urn:granit:grant_type:passkey", userId: assertion.UserId, userName: null,
                failureReason: "invalid_credentials", tenantId).ConfigureAwait(false);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        ImmutableArray<string> scopes = request.GetScopes();
        ClaimsPrincipal principal = await principalFactory.CreateUserPrincipalAsync(
            user, scopes, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .ConfigureAwait(false);

        metrics.RecordTokenIssued(tenantId, "urn:granit:grant_type:passkey");
        metrics.RecordAuthenticationSuccess(tenantId, "urn:granit:grant_type:passkey");
        LogTokenIssued(logger, user.Id.ToString(), "urn:granit:grant_type:passkey");

        await TryWriteAuthAuditAsync(context, logger,
            method: "urn:granit:grant_type:passkey",
            userId: user.Id.ToString(),
            userName: user.UserName,
            failureReason: null,
            tenantId).ConfigureAwait(false);

        return Results.SignIn(principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Records an authentication audit row for the token endpoint. No-op when
    /// <see cref="IAuditingWriter"/> is not registered. Audit failures are
    /// swallowed so a transient audit-store outage never breaks token issuance.
    /// </summary>
    private static async Task TryWriteAuthAuditAsync(
        HttpContext context,
        ILogger logger,
        string method,
        string? userId,
        string? userName,
        string? failureReason,
        string? tenantId)
    {
        IAuditingWriter? auditingWriter = context.RequestServices.GetService<IAuditingWriter>();
        if (auditingWriter is null)
        {
            return;
        }

        TimeProvider timeProvider = context.RequestServices.GetService<TimeProvider>()
            ?? TimeProvider.System;
        Guid? tenantGuid = !string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out Guid parsed)
            ? parsed : null;
        string? ipAddress = context.Connection.RemoteIpAddress?.ToString();
        string? userAgent = context.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }
        string? correlationId = Activity.Current?.Id;

        AuditEntry entry = failureReason is null
            ? AuthenticationAuditEntry.CreateSuccess(
                timeProvider.GetUtcNow(), userId!, userName, method,
                tenantGuid, ipAddress, userAgent, correlationId)
            : AuthenticationAuditEntry.CreateFailure(
                timeProvider.GetUtcNow(), userId, userName, method, failureReason,
                tenantGuid, ipAddress, userAgent, correlationId);

        try
        {
            await auditingWriter.WriteAsync(entry, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuditWriteFailed(logger, ex);
        }
    }

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Information, Message = "Token issued for subject '{Subject}' using grant type '{GrantType}'")]
    private static partial void LogTokenIssued(ILogger logger, string subject, string grantType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token authentication failed for grant type '{GrantType}'")]
    private static partial void LogAuthenticationFailed(ILogger logger, string grantType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token request denied: user '{Subject}' not found")]
    private static partial void LogUserNotFound(ILogger logger, string subject);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unsupported grant type '{GrantType}'")]
    private static partial void LogUnsupportedGrantType(ILogger logger, string grantType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Token endpoint: failed to write authentication audit entry — token flow continues.")]
    private static partial void LogAuditWriteFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Two-factor grant: missing required 'username' or 'code' parameter")]
    private static partial void LogTwoFactorMissingParams(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Two-factor grant: invalid code for user '{UserId}'")]
    private static partial void LogTwoFactorFailed(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Passkey grant: missing 'credential_json' parameter")]
    private static partial void LogPasskeyMissingCredential(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Passkey grant: assertion verification failed")]
    private static partial void LogPasskeyAssertionFailed(ILogger logger);
}
