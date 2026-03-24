using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Granit.Authentication.JwtBearer.Cognito.Authentication;

/// <summary>
/// Transforms AWS Cognito claims to map <c>cognito:groups</c>
/// to standard .NET <see cref="ClaimTypes.Role"/>.
/// </summary>
/// <remarks>
/// Cognito tokens include groups as a JSON array in the <c>cognito:groups</c> claim.
/// This transformer maps each group to a standard role claim so that
/// <see cref="ClaimsPrincipal.IsInRole"/> and <c>[Authorize(Roles = "...")]</c> work as expected.
/// </remarks>
public sealed class CognitoClaimsTransformation : IClaimsTransformation
{
    private const string CognitoGroupsClaim = "cognito:groups";

    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        // Cognito includes groups as separate claims with the same type "cognito:groups".
        // Materialize to avoid modifying the collection while enumerating.
        List<Claim> groupClaims = [.. identity.FindAll(CognitoGroupsClaim)];

        // Collect existing roles in a HashSet to avoid duplicates
        HashSet<string> existingRoles = [.. identity.FindAll(ClaimTypes.Role).Select(c => c.Value)];

        foreach (string groupValue in groupClaims
            .Select(groupClaim => groupClaim.Value)
            .Where(value => !string.IsNullOrEmpty(value) && existingRoles.Add(value)))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, groupValue));
        }

        return Task.FromResult(principal);
    }
}
