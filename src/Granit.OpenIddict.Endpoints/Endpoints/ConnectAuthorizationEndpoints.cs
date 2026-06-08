using System.Collections.Immutable;
using System.Security.Claims;
using Granit.DataFiltering;
using Granit.Domain;
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
            // Explicit / external consent: redirect to the consent page which shows the
            // requested scopes and, on accept, creates a permanent authorization via
            // POST /admin/oidc/authorizations before redirecting back to returnUrl.
            // On deny, the consent page redirects back with ?error=access_denied.
            if (!string.IsNullOrEmpty(options.ConsentPath))
            {
                string returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                LogConsentRedirect(logger, request.ClientId!, consentType ?? "(null)");
                return Results.Redirect($"{options.ConsentPath}?returnUrl={Uri.EscapeDataString(returnUrl)}");
            }

            LogExplicitConsentNotSupported(logger, request.ClientId!, consentType ?? "(null)");
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Resolve the user by primary key.
        // Bypass the multi-tenant filter: the authenticated user ID is globally unique
        // and the cookie signature already guarantees the principal's authenticity.
        // Leaving the filter active makes the lookup depend on whatever tenant context
        // has been resolved for this request (or leaked from prior state), which will
        // hide the user whenever their TenantId does not match the active scope —
        // host admins (TenantId = null) in particular become invisible on every
        // request that has inherited a tenant context from another source.
        UserManager<LocalIdentity> userManager = context.RequestServices
            .GetRequiredService<UserManager<LocalIdentity>>();
        IDataFilter? dataFilter = context.RequestServices.GetService<IDataFilter>();

        LocalIdentity? user;
        IDisposable? filterScope = dataFilter?.Disable<IMultiTenant>();
        try
        {
            user = await userManager.GetUserAsync(authenticateResult.Principal).ConfigureAwait(false);
        }
        finally
        {
            filterScope?.Dispose();
        }

        if (user is null)
        {
            // Stale Identity cookie referencing a user that no longer exists (deletion,
            // DB reseed, schema drop). Clear the cookie so the browser can recover —
            // otherwise every refresh re-presents the same cookie and loops on 403.
            LogUserNotFound(logger);
            await context.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);

            string returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            string effectiveLoginPath = request.ClientId is not null
                && options.ClientLoginPaths.TryGetValue(request.ClientId, out string? clientPath)
                ? clientPath
                : options.LoginPath;
            return Results.Redirect($"{effectiveLoginPath}?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        // Align the tenant context with the resolved user for the rest of the flow
        // (authorization-record lookup, principal build, metrics). When the user is
        // a host admin (TenantId = null) this clears any stale tenant leaked from
        // a prior request; when the user is a tenant user this switches the scope
        // to their own tenant regardless of what the resolver pipeline produced.
        ICurrentTenant? requestTenant = context.RequestServices.GetService<ICurrentTenant>();
        using IDisposable? tenantScope = requestTenant?.Change(user.TenantId);

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

        string? tenantId = requestTenant is { IsAvailable: true } ? requestTenant.Id?.ToString() : null;
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Authorization: redirecting to consent page for client '{ClientId}' (consent type: '{ConsentType}')")]
    private static partial void LogConsentRedirect(ILogger logger, string clientId, string consentType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorization: explicit consent not supported for client '{ClientId}' (consent type: '{ConsentType}') — ConsentPath is empty")]
    private static partial void LogExplicitConsentNotSupported(ILogger logger, string clientId, string consentType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorization: authenticated user not found in identity store")]
    private static partial void LogUserNotFound(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Authorization granted for subject '{Subject}' to client '{ClientId}'")]
    private static partial void LogAuthorizationGranted(ILogger logger, string subject, string clientId);
}
