using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Granit.Authentication.JwtBearer.EntraId.Authentication;

/// <summary>
/// Transforms Entra ID claims to map App Roles
/// to standard .NET <see cref="ClaimTypes.Role"/>.
/// </summary>
/// <remarks>
/// Entra ID v2.0 tokens carry App Roles as individual <c>roles</c> claims (one per role).
/// Entra ID v1.0 tokens may carry them as a single <c>roles</c> claim containing a JSON array.
/// This transformation handles both formats.
/// </remarks>
public sealed class EntraIdClaimsTransformation : IClaimsTransformation
{
    private const string RolesClaimType = "roles";

    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var rolesClaims = identity.FindAll(RolesClaimType).ToList();
        if (rolesClaims.Count == 0)
        {
            return Task.FromResult(principal);
        }

        // Collect existing roles in a HashSet to avoid O(n²) HasClaim scans
        HashSet<string> existingRoles = [.. identity.FindAll(ClaimTypes.Role).Select(c => c.Value)];

        IEnumerable<string> roleValues = rolesClaims.Select(c => c.Value);

        foreach (string value in roleValues)
        {
            if (value.StartsWith('['))
            {
                // v1.0 format: single claim with JSON array value
                AddRolesFromJsonArray(identity, value, existingRoles);
            }
            else
            {
                // v2.0 format: individual string claim per role
                AddRoleIfNew(identity, value, existingRoles);
            }
        }

        return Task.FromResult(principal);
    }

    private static void AddRolesFromJsonArray(
        ClaimsIdentity identity,
        string jsonArrayValue,
        HashSet<string> existingRoles)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonArrayValue);
            foreach (JsonElement element in doc.RootElement.EnumerateArray())
            {
                string? roleValue = element.GetString();
                if (!string.IsNullOrEmpty(roleValue))
                {
                    AddRoleIfNew(identity, roleValue, existingRoles);
                }
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — skip silently
        }
    }

    private static void AddRoleIfNew(
        ClaimsIdentity identity,
        string roleValue,
        HashSet<string> existingRoles)
    {
        if (existingRoles.Add(roleValue))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
        }
    }
}
