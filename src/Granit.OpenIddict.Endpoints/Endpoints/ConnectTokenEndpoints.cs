using System.Collections.Immutable;
using System.Security.Claims;
using Granit.Identity.Local.Domain;
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

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Information, Message = "Token issued for subject '{Subject}' using grant type '{GrantType}'")]
    private static partial void LogTokenIssued(ILogger logger, string subject, string grantType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token authentication failed for grant type '{GrantType}'")]
    private static partial void LogAuthenticationFailed(ILogger logger, string grantType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token request denied: user '{Subject}' not found")]
    private static partial void LogUserNotFound(ILogger logger, string subject);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unsupported grant type '{GrantType}'")]
    private static partial void LogUnsupportedGrantType(ILogger logger, string grantType);
}
