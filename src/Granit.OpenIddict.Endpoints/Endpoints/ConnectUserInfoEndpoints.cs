using System.Collections.Immutable;
using System.Security.Claims;
using Granit.DataFiltering;
using Granit.Identity.Local.Domain;
using Granit.MultiTenancy;
using Granit.OpenIddict.Endpoints.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Granit.OpenIddict.Endpoints.Endpoints;

/// <summary>
/// OIDC UserInfo endpoint (<c>GET /connect/userinfo</c>).
/// Returns user claims based on the granted scopes in the access token.
/// </summary>
#pragma warning disable GRAPI001 // OpenIddict requires Results.Forbid with explicit auth scheme
internal static class ConnectUserInfoEndpoints
{
    internal static IEndpointRouteBuilder MapConnectUserInfoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/connect/userinfo", (Delegate)HandleUserInfoAsync)
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<IResult> HandleUserInfoAsync(HttpContext context)
    {
        // OpenIddict has already validated the access token via the middleware.
        AuthenticateResult authenticateResult = await context.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);

        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        ClaimsPrincipal principal = authenticateResult.Principal;
        string? subject = principal.GetClaim(OpenIddictConstants.Claims.Subject);

        if (string.IsNullOrEmpty(subject))
        {
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Resolve the user with the multi-tenant filter disabled, then align the tenant scope to
        // the user. userinfo is called by a resource server with no tenant middleware, so a bare
        // lookup would fail closed for every tenant user (and leak nothing for host users).
        UserManager<LocalIdentity> userManager = context.RequestServices
            .GetRequiredService<UserManager<LocalIdentity>>();
        IDataFilter? dataFilter = context.RequestServices.GetService<IDataFilter>();
        ICurrentTenant? currentTenant = context.RequestServices.GetService<ICurrentTenant>();

        LocalIdentity? user = await OidcUserTenantResolver
            .FindBySubjectAsync(userManager, subject, dataFilter)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Align the tenant scope to the resolved user for the rest of the request. Opened here (not
        // inside the resolver) because ICurrentTenant is AsyncLocal-backed.
        using IDisposable? tenantScope = currentTenant?.Change(user.TenantId);

        // Build claims based on granted scopes.
        ImmutableArray<string> scopes = principal.GetScopes();
        Dictionary<string, object> claims = new(StringComparer.Ordinal)
        {
            [OpenIddictConstants.Claims.Subject] = subject,
        };

        if (scopes.Contains(OpenIddictConstants.Scopes.Profile))
        {
            if (user.UserName is not null)
            {
                claims[OpenIddictConstants.Claims.PreferredUsername] = user.UserName;
            }

            if (user.FirstName is not null || user.LastName is not null)
            {
                claims[OpenIddictConstants.Claims.Name] = $"{user.FirstName} {user.LastName}".Trim();
            }

            if (user.FirstName is not null)
            {
                claims[OpenIddictConstants.Claims.GivenName] = user.FirstName;
            }

            if (user.LastName is not null)
            {
                claims[OpenIddictConstants.Claims.FamilyName] = user.LastName;
            }
        }

        if (scopes.Contains(OpenIddictConstants.Scopes.Email) && user.Email is not null)
        {
            claims[OpenIddictConstants.Claims.Email] = user.Email;
            claims[OpenIddictConstants.Claims.EmailVerified] = user.EmailConfirmed;
        }

        if (scopes.Contains(OpenIddictConstants.Scopes.Phone) && user.PhoneNumber is not null)
        {
            claims[OpenIddictConstants.Claims.PhoneNumber] = user.PhoneNumber;
            claims[OpenIddictConstants.Claims.PhoneNumberVerified] = user.PhoneNumberConfirmed;
        }

        if (scopes.Contains(OpenIddictConstants.Scopes.Roles))
        {
            IList<string> roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
            if (roles.Count > 0)
            {
                claims[OpenIddictConstants.Claims.Role] = roles;
            }
        }

        return TypedResults.Ok(claims);
    }
}
