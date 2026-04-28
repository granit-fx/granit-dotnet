using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Granit.Authentication.Claims;

/// <summary>
/// Normalizes the OIDC short-name <c>role</c> claim emitted by OpenIddict.Validation
/// into <see cref="ClaimTypes.Role"/> so that consumers reading <see cref="ClaimTypes.Role"/>
/// (notably <c>Granit.Authorization.PermissionChecker</c>'s <c>AdminRoles</c> bypass and
/// <c>ICurrentUserService.GetRoles()</c>) agree on the source claim type with
/// <see cref="ClaimsPrincipal.IsInRole(string)"/>.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the normalization performed implicitly by <c>Granit.Authentication.JwtBearer</c>,
/// where <c>TokenValidationParameters.RoleClaimType = ClaimTypes.Role</c> forces the
/// JWT pipeline to map roles onto the URI claim type. OpenIddict.Validation does not go
/// through <c>JsonWebTokenHandler</c> when running with <c>UseLocalServer()</c>: the
/// principal is rebuilt from data-protection claims using OIDC short names. Without this
/// transformation, <c>User.IsInRole("Admin")</c> returns <c>true</c> (the BCL respects
/// <see cref="ClaimsIdentity.RoleClaimType"/>) but <c>User.FindAll(ClaimTypes.Role)</c>
/// returns empty.
/// </para>
/// <para>
/// The transformation is scoped to the <c>"OpenIddict.Validation.AspNetCore"</c>
/// authentication scheme. Principals from other schemes (cookies, JwtBearer, ApiKeys)
/// are returned unchanged so the transformation is safe to register globally.
/// </para>
/// </remarks>
public sealed class OpenIddictRoleClaimsTransformation : IClaimsTransformation
{
    internal const string OpenIddictValidationScheme = "OpenIddict.Validation.AspNetCore";
    internal const string OidcRoleClaimType = "role";

    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        if (!string.Equals(
                identity.AuthenticationType,
                OpenIddictValidationScheme,
                StringComparison.Ordinal))
        {
            return Task.FromResult(principal);
        }

        HashSet<string> existing = [.. identity.FindAll(ClaimTypes.Role).Select(c => c.Value)];

        // Materialize the source claims before mutating the identity. ClaimsIdentity.FindAll
        // returns an enumerator over the live claims collection — calling AddClaim mid-iteration
        // throws InvalidOperationException.
        List<Claim> sourceClaims = [.. identity.FindAll(OidcRoleClaimType)];
        foreach (Claim claim in sourceClaims)
        {
            if (existing.Add(claim.Value))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, claim.Value));
            }
        }

        return Task.FromResult(principal);
    }
}
