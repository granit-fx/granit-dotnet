using System.Security.Claims;
using System.Text.Json;
using Granit.Authentication.JwtBearer.GoogleCloud.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.JwtBearer.GoogleCloud.Authentication;

/// <summary>
/// Transforms Google Cloud Identity Platform (Firebase Auth) claims to map
/// custom claims roles to standard .NET <see cref="ClaimTypes.Role"/>.
/// </summary>
/// <remarks>
/// Firebase Auth tokens carry custom claims as top-level properties in the JWT payload.
/// The roles claim key is configurable (default: <c>"roles"</c>) and its value is
/// expected to be a JSON array of role strings.
/// </remarks>
public sealed class GoogleCloudClaimsTransformation(
    IOptions<GoogleCloudAuthenticationOptions> options) : IClaimsTransformation
{
    private readonly IOptions<GoogleCloudAuthenticationOptions> _options = options;

    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        GoogleCloudAuthenticationOptions opts = _options.Value;
        Claim? rolesClaim = identity.FindFirst(opts.RolesClaimKey);
        if (rolesClaim is null)
        {
            return Task.FromResult(principal);
        }

        // Collect existing roles in a HashSet to avoid O(n²) HasClaim scans
        HashSet<string> existingRoles = [.. identity.FindAll(ClaimTypes.Role).Select(c => c.Value)];

        string value = rolesClaim.Value;

        if (value.StartsWith('['))
        {
            // JSON array format: ["admin", "editor"]
            AddRolesFromJsonArray(identity, value, existingRoles);
        }
        else if (!string.IsNullOrEmpty(value))
        {
            // Single string value
            AddRoleIfNew(identity, value, existingRoles);
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
