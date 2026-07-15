using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Claims;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.DataFiltering;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Endpoints.Internal;
using Granit.OpenIddict.Services;
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
    private const string LoggerCategory = "Granit.OpenIddict.Endpoints.ConnectTokenEndpoints";
    private const string TwoFactorGrantType = "urn:granit:grant_type:two_factor";
    private const string PasskeyGrantType = "urn:granit:grant_type:passkey";
    private const string InvalidLoginReason = "invalid_credentials";

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

        OpenIddictMetrics metrics = context.RequestServices
            .GetRequiredService<OpenIddictMetrics>();
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(LoggerCategory);
        ICurrentTenant? currentTenant = context.RequestServices
            .GetService<ICurrentTenant>();
        string? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id?.ToString() : null;

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            return await HandleCodeOrRefreshAsync(
                context, request, metrics, tenantId)
                .ConfigureAwait(false);
        }

        if (request.IsClientCredentialsGrantType())
        {
            return HandleClientCredentials(context, request, metrics, tenantId);
        }

        if (request.GrantType == TwoFactorGrantType)
        {
            return await HandleTwoFactorAsync(
                context, request, metrics, tenantId)
                .ConfigureAwait(false);
        }

        if (request.GrantType == PasskeyGrantType)
        {
            return await HandlePasskeyAsync(
                context, request, metrics, tenantId)
                .ConfigureAwait(false);
        }

        LogUnsupportedGrantType(logger, request.GrantType ?? "(null)");
        return Results.Forbid(
            authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> HandleCodeOrRefreshAsync(
        HttpContext context,
        OpenIddictRequest request,
        OpenIddictMetrics metrics,
        string? tenantId)
    {
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(LoggerCategory);

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
                metrics.RecordAuthenticationFailure(tenantId, InvalidLoginReason);
                await TryWriteAuthAuditAsync(context, logger,
                    method: request.GrantType!, userId: subject, userName: null,
                    failureReason: InvalidLoginReason, tenantId).ConfigureAwait(false);
                return Results.Forbid(
                    authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            // Rebuild the principal with up-to-date claims.
            // Preserve scopes from the original principal (including offline_access for refresh tokens).
            ImmutableArray<string> scopes = authenticateResult.Principal.GetScopes();

            IOidcPrincipalFactory principalFactory = context.RequestServices
                .GetRequiredService<IOidcPrincipalFactory>();
            ClaimsPrincipal principal = await principalFactory.CreateUserPrincipalAsync(
                user, scopes, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
                .ConfigureAwait(false);

            string grantType = request.GrantType!;
            metrics.RecordTokenIssued(tenantId, grantType);
            metrics.RecordAuthenticationSuccess(tenantId, grantType);
            LogTokenIssued(logger, user.Id.ToString(), grantType);

            // No success audit here: authorization_code / refresh_token do not authenticate the user at
            // the token endpoint — they exchange an authentication that already happened and was audited
            // at its source (the BFF session callback, or the interactive login endpoint for direct
            // clients). Auditing the exchange would duplicate that record. Token-level FAILURES above are
            // still audited, as a forged/replayed code or refresh token is a security event seen only here.

            string? ipAddress = context.Connection.RemoteIpAddress?.ToString();
            string? userAgent = context.Request.Headers.UserAgent.FirstOrDefault();
            if (string.IsNullOrEmpty(userAgent))
            {
                userAgent = null;
            }

            var properties = new AuthenticationProperties();
            if (ipAddress is not null)
            {
                properties.Items["ip_address"] = ipAddress;
            }

            if (userAgent is not null)
            {
                properties.Items["user_agent"] = userAgent;
            }

            // Preserve the persistent-login choice across refreshes: the source principal (auth code
            // or prior refresh token) carries remember_me. Re-stamp it as a claim (so the next refresh
            // and the heartbeat still see it) and as a token property (so the idle-session job can
            // exempt the session from inactivity revocation).
            if (authenticateResult.Principal.GetClaim("remember_me") is "true")
            {
                Claim rememberMe = new("remember_me", "true");
                rememberMe.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
                principal.Identities.First().AddClaim(rememberMe);
                properties.Items["remember_me"] = "true";
            }

            return Results.SignIn(principal, properties,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
            .CreateLogger(LoggerCategory);

        IOidcPrincipalFactory principalFactory = context.RequestServices
            .GetRequiredService<IOidcPrincipalFactory>();
        ClaimsPrincipal principal = principalFactory.CreateClientPrincipal(
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
        OpenIddictMetrics metrics,
        string? tenantId)
    {
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(LoggerCategory);

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

        // Resolve with the multi-tenant filter disabled, then align the scope to the user so the
        // recovery-code redemption and token verification below run under the user's own tenant.
        IDataFilter? dataFilter = context.RequestServices.GetService<IDataFilter>();
        ICurrentTenant? currentTenant = context.RequestServices.GetService<ICurrentTenant>();

        LocalIdentity? user = await OidcUserTenantResolver
            .FindByUserNameAsync(userManager, username, dataFilter)
            .ConfigureAwait(false);
        if (user is null)
        {
            LogUserNotFound(logger, username);
            metrics.RecordAuthenticationFailure(tenantId, InvalidLoginReason);
            await TryWriteAuthAuditAsync(context, logger,
                method: TwoFactorGrantType, userId: null, userName: username,
                failureReason: InvalidLoginReason, tenantId).ConfigureAwait(false);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Align the scope to the user so the recovery-code redemption and token verification below
        // run under the user's own tenant. Opened here (not in the resolver) because ICurrentTenant
        // is AsyncLocal-backed.
        using IDisposable? tenantScope = currentTenant?.Change(user.TenantId);

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
                method: TwoFactorGrantType, userId: user.Id.ToString(), userName: user.UserName,
                failureReason: "invalid_two_factor_code", tenantId).ConfigureAwait(false);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        ImmutableArray<string> scopes = request.GetScopes();
        IOidcPrincipalFactory principalFactory = context.RequestServices.GetRequiredService<IOidcPrincipalFactory>();
        ClaimsPrincipal principal = await principalFactory.CreateUserPrincipalAsync(
            user, scopes, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .ConfigureAwait(false);

        metrics.RecordTokenIssued(tenantId, TwoFactorGrantType);
        metrics.RecordAuthenticationSuccess(tenantId, TwoFactorGrantType);
        LogTokenIssued(logger, user.Id.ToString(), TwoFactorGrantType);

        await TryWriteAuthAuditAsync(context, logger,
            method: TwoFactorGrantType,
            userId: user.Id.ToString(),
            userName: user.UserName,
            failureReason: null,
            tenantId).ConfigureAwait(false);

        string? ipAddress = context.Connection.RemoteIpAddress?.ToString();
        string? userAgent = context.Request.Headers.UserAgent.FirstOrDefault();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }

        var properties = new AuthenticationProperties();
        if (ipAddress is not null)
        {
            properties.Items["ip_address"] = ipAddress;
        }

        if (userAgent is not null)
        {
            properties.Items["user_agent"] = userAgent;
        }

        return Results.SignIn(principal, properties,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> HandlePasskeyAsync(
        HttpContext context,
        OpenIddictRequest request,
        OpenIddictMetrics metrics,
        string? tenantId)
    {
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(LoggerCategory);

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
                method: PasskeyGrantType, userId: null, userName: null,
                failureReason: "invalid_passkey", tenantId).ConfigureAwait(false);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        UserManager<LocalIdentity> userManager = context.RequestServices
            .GetRequiredService<UserManager<LocalIdentity>>();
        IDataFilter? dataFilter = context.RequestServices.GetService<IDataFilter>();
        ICurrentTenant? currentTenant = context.RequestServices.GetService<ICurrentTenant>();

        LocalIdentity? user = await OidcUserTenantResolver
            .FindBySubjectAsync(userManager, assertion.UserId, dataFilter)
            .ConfigureAwait(false);
        if (user is null)
        {
            LogUserNotFound(logger, assertion.UserId);
            metrics.RecordAuthenticationFailure(tenantId, InvalidLoginReason);
            await TryWriteAuthAuditAsync(context, logger,
                method: PasskeyGrantType, userId: assertion.UserId, userName: null,
                failureReason: InvalidLoginReason, tenantId).ConfigureAwait(false);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Align the scope to the user for the principal build. Opened here (not in the resolver)
        // because ICurrentTenant is AsyncLocal-backed.
        using IDisposable? tenantScope = currentTenant?.Change(user.TenantId);

        ImmutableArray<string> scopes = request.GetScopes();
        IOidcPrincipalFactory principalFactory = context.RequestServices.GetRequiredService<IOidcPrincipalFactory>();
        ClaimsPrincipal principal = await principalFactory.CreateUserPrincipalAsync(
            user, scopes, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .ConfigureAwait(false);

        metrics.RecordTokenIssued(tenantId, PasskeyGrantType);
        metrics.RecordAuthenticationSuccess(tenantId, PasskeyGrantType);
        LogTokenIssued(logger, user.Id.ToString(), PasskeyGrantType);

        await TryWriteAuthAuditAsync(context, logger,
            method: PasskeyGrantType,
            userId: user.Id.ToString(),
            userName: user.UserName,
            failureReason: null,
            tenantId).ConfigureAwait(false);

        string? ipAddress = context.Connection.RemoteIpAddress?.ToString();
        string? userAgent = context.Request.Headers.UserAgent.FirstOrDefault();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }

        var properties = new AuthenticationProperties();
        if (ipAddress is not null)
        {
            properties.Items["ip_address"] = ipAddress;
        }

        if (userAgent is not null)
        {
            properties.Items["user_agent"] = userAgent;
        }

        return Results.SignIn(principal, properties,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
