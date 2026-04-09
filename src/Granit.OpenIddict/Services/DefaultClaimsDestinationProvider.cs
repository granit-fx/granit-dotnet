using System.Security.Claims;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Services;

/// <summary>
/// Default claims destination provider following standard OIDC conventions.
/// </summary>
/// <remarks>
/// <para>Rules:</para>
/// <list type="bullet">
/// <item><c>sub</c> (subject) → access_token + id_token (always)</item>
/// <item><c>name</c>, <c>given_name</c>, <c>family_name</c> → both if <c>profile</c> scope granted</item>
/// <item><c>email</c>, <c>email_verified</c> → both if <c>email</c> scope granted</item>
/// <item><c>role</c> → both if <c>roles</c> scope granted</item>
/// <item><c>phone_number</c>, <c>phone_number_verified</c> → both if <c>phone</c> scope granted</item>
/// <item><c>security_stamp</c> → excluded from all tokens</item>
/// <item><c>impersonator_id</c>, <c>impersonator_name</c> → access_token only</item>
/// <item>All other claims → access_token only</item>
/// </list>
/// <para>
/// Override by registering a custom <see cref="IClaimsDestinationProvider"/>
/// before <c>AddGranitOpenIddict()</c>.
/// </para>
/// </remarks>
#pragma warning disable GRSEC003 // Claim type and destination constants, not secrets
public class DefaultClaimsDestinationProvider : IClaimsDestinationProvider
{
    private static readonly HashSet<string> ExcludedClaims =
    [
        "AspNet.Identity.SecurityStamp",
        "security_stamp",
    ];

    private static readonly HashSet<string> ProfileClaims =
    [
        ClaimTypes.Name,
        ClaimTypes.GivenName,
        ClaimTypes.Surname,
        "name",
        "given_name",
        "family_name",
        "preferred_username",
    ];

    private static readonly HashSet<string> EmailClaims =
    [
        ClaimTypes.Email,
        "email",
        "email_verified",
    ];

    private static readonly HashSet<string> RoleClaims =
    [
        ClaimTypes.Role,
        "role",
    ];

    private static readonly HashSet<string> PhoneClaims =
    [
        ClaimTypes.MobilePhone,
        ClaimTypes.HomePhone,
        ClaimTypes.OtherPhone,
        "phone_number",
        "phone_number_verified",
    ];

    /// <inheritdoc/>
    public virtual IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(principal);

        return GetDestinationsCore(claim, principal);
    }

    private static IEnumerable<string> GetDestinationsCore(Claim claim, ClaimsPrincipal principal)
    {
        // Excluded claims — never in any token
        if (ExcludedClaims.Contains(claim.Type))
        {
            yield break;
        }

        // Subject and tenant — always in both tokens
        if (claim.Type is ClaimTypes.NameIdentifier or "sub" or "tenant_id")
        {
            yield return ClaimsDestinations.AccessToken;
            yield return ClaimsDestinations.IdentityToken;
            yield break;
        }

        // Profile claims — both if profile scope granted
        if (ProfileClaims.Contains(claim.Type))
        {
            yield return ClaimsDestinations.AccessToken;
            if (HasScope(principal, OpenIddictConstants.Scopes.Profile))
            {
                yield return ClaimsDestinations.IdentityToken;
            }

            yield break;
        }

        // Email claims — both if email scope granted
        if (EmailClaims.Contains(claim.Type))
        {
            yield return ClaimsDestinations.AccessToken;
            if (HasScope(principal, OpenIddictConstants.Scopes.Email))
            {
                yield return ClaimsDestinations.IdentityToken;
            }

            yield break;
        }

        // Role claims — both if roles scope granted
        if (RoleClaims.Contains(claim.Type))
        {
            yield return ClaimsDestinations.AccessToken;
            if (HasScope(principal, OpenIddictConstants.Scopes.Roles))
            {
                yield return ClaimsDestinations.IdentityToken;
            }

            yield break;
        }

        // Phone claims — both if phone scope granted
        if (PhoneClaims.Contains(claim.Type))
        {
            yield return ClaimsDestinations.AccessToken;
            if (HasScope(principal, OpenIddictConstants.Scopes.Phone))
            {
                yield return ClaimsDestinations.IdentityToken;
            }

            yield break;
        }

        // All other claims — access_token only
        yield return ClaimsDestinations.AccessToken;
    }

    private static bool HasScope(ClaimsPrincipal principal, string scope) =>
        principal.HasScope(scope);
}
#pragma warning restore GRSEC003
