using System.Security.Claims;
using System.Text.Json;
using Granit.Authentication.JwtBearer.Keycloak.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.JwtBearer.Keycloak.Authentication;

/// <summary>
/// Transforms Keycloak claims to map roles
/// to standard .NET <see cref="ClaimTypes.Role"/>.
/// </summary>
public sealed class KeycloakClaimsTransformation(IOptions<KeycloakOptions> options) : IClaimsTransformation
{
    private const string RolesProperty = "roles";

    private readonly IOptions<KeycloakOptions> _options = options;

    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        KeycloakOptions opts = _options.Value;
        Claim? accessClaim = identity.FindFirst(opts.RoleClaimsSource);
        if (accessClaim is null)
        {
            return Task.FromResult(principal);
        }

        using var doc = JsonDocument.Parse(accessClaim.Value);
        JsonElement root = doc.RootElement;

        // For resource_access, descend into the ClientId node before "roles"
        if (opts.RoleClaimsSource == "resource_access"
            && !string.IsNullOrEmpty(opts.ClientId)
            && !root.TryGetProperty(opts.ClientId, out root))
        {
            return Task.FromResult(principal);
        }

        if (!root.TryGetProperty(RolesProperty, out JsonElement rolesElement))
        {
            return Task.FromResult(principal);
        }

        // Collect existing roles in a HashSet to avoid O(n²) HasClaim scans
        HashSet<string> existingRoles = [.. identity.FindAll(ClaimTypes.Role).Select(c => c.Value)];

        foreach (JsonElement role in rolesElement.EnumerateArray())
        {
            string? roleValue = role.GetString();
            if (!string.IsNullOrEmpty(roleValue) && existingRoles.Add(roleValue))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
            }
        }

        return Task.FromResult(principal);
    }
}
