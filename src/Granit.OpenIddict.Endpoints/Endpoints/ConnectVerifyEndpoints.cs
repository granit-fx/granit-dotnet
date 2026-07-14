using System.Collections.Immutable;
using System.Security.Claims;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity.Local.Domain;
using Granit.OpenIddict.Endpoints.Options;
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
/// Device authorization verification endpoint (<c>GET/POST /connect/verify</c>).
/// Handles the end-user verification step of the OAuth 2.0 Device Authorization Grant (RFC 8628).
/// </summary>
/// <remarks>
/// <para>
/// When a device requests a token via <c>/connect/device</c>, it receives a
/// <c>user_code</c> and a <c>verification_uri</c>. The user navigates to
/// <c>/connect/verify</c>, enters the code, and authorizes the device.
/// </para>
/// <para>
/// Flow:
/// <list type="number">
///   <item>No <c>user_code</c> → redirect to <see cref="OpenIddictServerEndpointsOptions.DeviceVerificationPath"/>.</item>
///   <item>Invalid/expired <c>user_code</c> → OpenID Connect error response (400).</item>
///   <item>Valid <c>user_code</c> but user not authenticated → redirect to login, preserving <c>user_code</c> in returnUrl.</item>
///   <item>Valid <c>user_code</c> + authenticated user → build principal, call SignIn to mark the device as authorized.</item>
/// </list>
/// </para>
/// </remarks>
#pragma warning disable GRAPI001 // OpenIddict requires Results.SignIn/Challenge with explicit auth scheme
#pragma warning disable GRAPI003 // Private handler methods — not exposed as endpoint parameters
internal static partial class ConnectVerifyEndpoints
{
    internal static IEndpointRouteBuilder MapConnectVerifyEndpoints(
        this IEndpointRouteBuilder endpoints,
        OpenIddictServerEndpointsOptions options)
    {
        endpoints.MapGet("/connect/verify",
                (Delegate)((HttpContext context) => HandleVerifyAsync(context, options)))
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapPost("/connect/verify",
                (Delegate)((HttpContext context) => HandleVerifyAsync(context, options)))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<IResult> HandleVerifyAsync(
        HttpContext context,
        OpenIddictServerEndpointsOptions options)
    {
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.OpenIddict.Endpoints.ConnectVerifyEndpoints");

        OpenIddictRequest? request = context.GetOpenIddictServerRequest();

        // No user_code present yet — redirect to the host's device verification page.
        if (request is null || string.IsNullOrEmpty((string?)request["user_code"]))
        {
            string path = options.DeviceVerificationPath;
            LogDeviceVerificationRedirect(logger, path);
            return Results.Redirect(path);
        }

        string userCode = (string?)request["user_code"] ?? string.Empty;

        // Validate the user_code by authenticating with the OpenIddict scheme.
        // OpenIddict looks up the device authorization in the store.
        // If the code is invalid or expired, authentication fails.
        AuthenticateResult oidcResult = await context.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);

        if (!oidcResult.Succeeded || oidcResult.Principal is null)
        {
            LogInvalidUserCode(logger, userCode);
            return Results.Challenge(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                        OpenIddictConstants.Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The specified user code is invalid or has expired."
                }),
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Require the end-user to be authenticated before authorizing the device (RFC 8628 §3.3).
        AuthenticateResult identityResult = await context.AuthenticateAsync(
            IdentityConstants.ApplicationScheme).ConfigureAwait(false);

        if (!identityResult.Succeeded || identityResult.Principal is null)
        {
            string verifyReturnUrl = $"{options.DeviceVerificationPath}?user_code={Uri.EscapeDataString(userCode)}";
            string loginUrl = $"{options.LoginPath}?returnUrl={Uri.EscapeDataString(verifyReturnUrl)}";
            LogUnauthenticatedDeviceVerification(logger, loginUrl);
            return Results.Redirect(loginUrl);
        }

        // Resolve the user — bypass the multi-tenant filter: the authenticated user ID is globally
        // unique and the cookie signature already guarantees authenticity.
        UserManager<LocalIdentity> userManager = context.RequestServices
            .GetRequiredService<UserManager<LocalIdentity>>();
        IDataFilter? dataFilter = context.RequestServices.GetService<IDataFilter>();

        LocalIdentity? user;
        IDisposable? filterScope = dataFilter?.Disable<IMultiTenant>();
        try
        {
            user = await userManager.GetUserAsync(identityResult.Principal).ConfigureAwait(false);
        }
        finally
        {
            filterScope?.Dispose();
        }

        if (user is null)
        {
            // Stale cookie — clear and redirect to login.
            await context.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
            string verifyReturnUrl = $"{options.DeviceVerificationPath}?user_code={Uri.EscapeDataString(userCode)}";
            return Results.Redirect($"{options.LoginPath}?returnUrl={Uri.EscapeDataString(verifyReturnUrl)}");
        }

        // Build a principal from the authenticated user with the device authorization's requested scopes.
        // The principal from oidcResult carries the scopes originally requested by the device.
        ImmutableArray<string> scopes = oidcResult.Principal.GetScopes();
        IOidcPrincipalFactory principalFactory = context.RequestServices
            .GetRequiredService<IOidcPrincipalFactory>();

        ClaimsPrincipal principal = await principalFactory.CreateUserPrincipalAsync(
            user, scopes, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            context.RequestAborted).ConfigureAwait(false);

        LogDeviceAuthorizationApproved(logger, user.Id.ToString());

        // SignIn marks the device authorization as approved for this user.
        // OpenIddict updates the pending device authorization so the device can exchange it for tokens
        // via POST /connect/token with grant_type=urn:ietf:params:oauth:grant-type:device_code.
        return Results.SignIn(principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Device verification: redirecting to '{Path}'")]
    private static partial void LogDeviceVerificationRedirect(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Device verification: invalid or expired user code '{UserCode}'")]
    private static partial void LogInvalidUserCode(ILogger logger, string userCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Device verification: unauthenticated user redirected to login at '{LoginUrl}'")]
    private static partial void LogUnauthenticatedDeviceVerification(ILogger logger, string loginUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Device verification: approved for user '{UserId}'")]
    private static partial void LogDeviceAuthorizationApproved(ILogger logger, string userId);
}
