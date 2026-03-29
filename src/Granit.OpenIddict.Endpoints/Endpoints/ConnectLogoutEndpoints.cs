using Granit.OpenIddict.Endpoints.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Server.AspNetCore;

namespace Granit.OpenIddict.Endpoints.Endpoints;

/// <summary>
/// OIDC end-session endpoint (<c>GET/POST /connect/logout</c>).
/// Signs out the Identity cookie and lets OpenIddict handle the post-logout redirect.
/// </summary>
#pragma warning disable GRAPI001 // OpenIddict requires Results.SignOut with explicit auth scheme
internal static class ConnectLogoutEndpoints
{
    internal static IEndpointRouteBuilder MapConnectLogoutEndpoints(
        this IEndpointRouteBuilder endpoints,
        OpenIddictServerEndpointsOptions options)
    {
        endpoints.MapMethods("/connect/logout", ["GET", "POST"],
                (Delegate)((HttpContext context) => HandleLogoutAsync(context, options)))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<IResult> HandleLogoutAsync(
        HttpContext context,
        OpenIddictServerEndpointsOptions options)
    {
        // Sign out the Identity application cookie.
        await context.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);

        // Let OpenIddict handle post_logout_redirect_uri validation and redirect.
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = options.PostLogoutRedirectPath },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }
}
