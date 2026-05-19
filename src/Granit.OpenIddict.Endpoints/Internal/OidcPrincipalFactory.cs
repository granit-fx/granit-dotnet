using System.Collections.Immutable;
using System.Security.Claims;
using Granit.Identity.Local.Domain;
using Granit.OpenIddict.Extensions;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Endpoints.Internal;

/// <summary>
/// Builds a <see cref="ClaimsPrincipal"/> from a <see cref="LocalIdentity"/> for OIDC token issuance.
/// Isolated from OpenIddict-specific types so the logic is reusable with other OIDC providers.
/// </summary>
internal sealed class OidcPrincipalFactory(
    UserManager<LocalIdentity> userManager,
    IClaimsDestinationProvider destinationProvider)
{
    /// <summary>
    /// Creates a <see cref="ClaimsPrincipal"/> with standard OIDC claims from the user,
    /// sets requested scopes, and applies claim destinations.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="scopes">The requested OIDC scopes.</param>
    /// <param name="authenticationScheme">The authentication scheme for the <see cref="ClaimsIdentity"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A principal ready for token issuance.</returns>
    internal async Task<ClaimsPrincipal> CreateUserPrincipalAsync(
        LocalIdentity user,
        ImmutableArray<string> scopes,
        string authenticationScheme,
        CancellationToken cancellationToken = default)
    {
        ClaimsIdentity identity = new(authenticationScheme);

        // Subject (mandatory)
        identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());

        // Profile claims
        if (user.UserName is not null)
        {
            identity.AddClaim(OpenIddictConstants.Claims.PreferredUsername, user.UserName);
        }

        if (user.FirstName is not null || user.LastName is not null)
        {
            string fullName = $"{user.FirstName} {user.LastName}".Trim();
            identity.AddClaim(OpenIddictConstants.Claims.Name, fullName);
        }

        if (user.FirstName is not null)
        {
            identity.AddClaim(OpenIddictConstants.Claims.GivenName, user.FirstName);
        }

        if (user.LastName is not null)
        {
            identity.AddClaim(OpenIddictConstants.Claims.FamilyName, user.LastName);
        }

        // Email
        if (user.Email is not null)
        {
            identity.AddClaim(OpenIddictConstants.Claims.Email, user.Email);
            identity.AddClaim(OpenIddictConstants.Claims.EmailVerified,
                user.EmailConfirmed ? "true" : "false");
        }

        // Phone
        if (user.PhoneNumber is not null)
        {
            identity.AddClaim(OpenIddictConstants.Claims.PhoneNumber, user.PhoneNumber);
            identity.AddClaim(OpenIddictConstants.Claims.PhoneNumberVerified,
                user.PhoneNumberConfirmed ? "true" : "false");
        }

        // Roles
        IList<string> roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        foreach (string role in roles)
        {
            identity.AddClaim(OpenIddictConstants.Claims.Role, role);
        }

        // Tenant
        if (user.TenantId is not null)
        {
            identity.AddClaim("tenant_id", user.TenantId.Value.ToString());
        }

        // Custom claims from UserManager store
        IList<Claim> customClaims = await userManager.GetClaimsAsync(user).ConfigureAwait(false);
        identity.AddClaims(customClaims);

        // Scopes
        identity.SetScopes(scopes);

        // Destinations
        ClaimsPrincipal principal = new(identity);
        principal.SetDestinations(destinationProvider);

        return principal;
    }

    /// <summary>
    /// Creates a <see cref="ClaimsPrincipal"/> for client credentials (no user).
    /// </summary>
    internal static ClaimsPrincipal CreateClientPrincipal(
        string clientId,
        ImmutableArray<string> scopes,
        string authenticationScheme)
    {
        ClaimsIdentity identity = new(authenticationScheme);
        identity.AddClaim(OpenIddictConstants.Claims.Subject, clientId);
        identity.SetScopes(scopes);
        identity.SetDestinations(static _ => [OpenIddictConstants.Destinations.AccessToken]);

        return new ClaimsPrincipal(identity);
    }
}
