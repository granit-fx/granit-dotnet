using System.Collections.Immutable;
using System.Security.Claims;
using Granit.Identity.Local.Domain;
using Granit.MultiTenancy;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Endpoints.Internal;
using Granit.OpenIddict.Endpoints.Options;
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
/// OIDC authorization endpoint (<c>GET/POST /connect/authorize</c>).
/// Validates user authentication, auto-grants consent for implicit clients,
/// and issues an authorization code via OpenIddict.
/// </summary>
#pragma warning disable GRAPI001 // OpenIddict requires Results.SignIn/Forbid/Challenge with explicit auth scheme
internal static partial class ConnectAuthorizationEndpoints
{
    internal static IEndpointRouteBuilder MapConnectAuthorizationEndpoints(
        this IEndpointRouteBuilder endpoints,
        OpenIddictServerEndpointsOptions options)
    {
        endpoints.MapMethods("/connect/authorize", ["GET", "POST"],
                (Delegate)((HttpContext context) => HandleAuthorizeAsync(context, options)))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<IResult> HandleAuthorizeAsync(
        HttpContext context,
        OpenIddictServerEndpointsOptions options)
    {
        OpenIddictRequest request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict server request is not available.");

        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.OpenIddict.Endpoints.ConnectAuthorizationEndpoints");

        // Authenticate the user via the Identity cookie.
        AuthenticateResult authenticateResult = await context.AuthenticateAsync(
            IdentityConstants.ApplicationScheme).ConfigureAwait(false);

        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            // Not authenticated — redirect to the configured login page.
            // In headless/BFF mode this points to the SPA login route; in MVC mode
            // it points to a Razor page. The returnUrl lets the login page redirect
            // back to /connect/authorize after successful authentication.
            string returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            string effectiveLoginPath = request.ClientId is not null
                && options.ClientLoginPaths.TryGetValue(request.ClientId, out string? clientPath)
                ? clientPath
                : options.LoginPath;
            string loginUrl = $"{effectiveLoginPath}?returnUrl={Uri.EscapeDataString(returnUrl)}";

            LogUnauthenticatedRedirect(logger, request.ClientId ?? "(null)");

            return Results.Redirect(loginUrl);
        }

        // Resolve the application to check consent type.
        IOpenIddictApplicationManager applicationManager = context.RequestServices
            .GetRequiredService<IOpenIddictApplicationManager>();

        object? application = await applicationManager
            .FindByClientIdAsync(request.ClientId!, context.RequestAborted)
            .ConfigureAwait(false);

        if (application is null)
        {
            LogApplicationNotFound(logger, request.ClientId!);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Check consent type — only implicit and systematic are auto-granted in v1.
        string? consentType = await applicationManager
            .GetConsentTypeAsync(application, context.RequestAborted)
            .ConfigureAwait(false);

        if (!string.Equals(consentType, OpenIddictConstants.ConsentTypes.Implicit, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(consentType, OpenIddictConstants.ConsentTypes.Systematic, StringComparison.OrdinalIgnoreCase))
        {
            LogExplicitConsentNotSupported(logger, request.ClientId!, consentType ?? "(null)");
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Resolve the user.
        UserManager<GranitUser> userManager = context.RequestServices
            .GetRequiredService<UserManager<GranitUser>>();
        GranitUser? user = await userManager
            .GetUserAsync(authenticateResult.Principal)
            .ConfigureAwait(false);

        if (user is null)
        {
            LogUserNotFound(logger);
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Build the principal with requested scopes.
        ImmutableArray<string> scopes = request.GetScopes();
        OidcPrincipalFactory principalFactory = context.RequestServices
            .GetRequiredService<OidcPrincipalFactory>();

        ClaimsPrincipal principal = await principalFactory
            .CreateUserPrincipalAsync(
                user, scopes, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                context.RequestAborted)
            .ConfigureAwait(false);

        // Reuse or create an authorization for this subject + client + scopes.
        IOpenIddictAuthorizationManager authorizationManager = context.RequestServices
            .GetRequiredService<IOpenIddictAuthorizationManager>();
        string? applicationId = await applicationManager
            .GetIdAsync(application, context.RequestAborted)
            .ConfigureAwait(false);

        object? authorization = null;
        List<object> authorizations = [];

        await foreach (object auth in authorizationManager.FindAsync(
            subject: user.Id.ToString(),
            client: applicationId!,
            status: OpenIddictConstants.Statuses.Valid,
            type: OpenIddictConstants.AuthorizationTypes.Permanent,
            scopes: scopes,
            cancellationToken: context.RequestAborted).ConfigureAwait(false))
        {
            authorizations.Add(auth);
        }

        authorization = authorizations.FirstOrDefault();

        if (authorization is null)
        {
            authorization = await authorizationManager.CreateAsync(
                principal: principal,
                subject: user.Id.ToString(),
                client: applicationId!,
                type: OpenIddictConstants.AuthorizationTypes.Permanent,
                scopes: scopes,
                cancellationToken: context.RequestAborted).ConfigureAwait(false);
        }

        string? authorizationId = await authorizationManager
            .GetIdAsync(authorization, context.RequestAborted)
            .ConfigureAwait(false);
        principal.SetAuthorizationId(authorizationId);

        LogAuthorizationGranted(logger, user.Id.ToString(), request.ClientId!);

        ICurrentTenant? currentTenant = context.RequestServices.GetService<ICurrentTenant>();
        string? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id?.ToString() : null;
        OpenIddictMetrics metrics = context.RequestServices.GetRequiredService<OpenIddictMetrics>();
        metrics.RecordAuthenticationSuccess(tenantId, "authorization_code");

        return Results.SignIn(principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Debug, Message = "Authorization: unauthenticated user redirected to login for client '{ClientId}'")]
    private static partial void LogUnauthenticatedRedirect(ILogger logger, string clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorization: application '{ClientId}' not found")]
    private static partial void LogApplicationNotFound(ILogger logger, string clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorization: explicit consent not supported for client '{ClientId}' (consent type: '{ConsentType}')")]
    private static partial void LogExplicitConsentNotSupported(ILogger logger, string clientId, string consentType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorization: authenticated user not found in identity store")]
    private static partial void LogUserNotFound(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Authorization granted for subject '{Subject}' to client '{ClientId}'")]
    private static partial void LogAuthorizationGranted(ILogger logger, string subject, string clientId);
}
