namespace Granit.Authentication.JwtBearer.Keycloak.Options;

/// <summary>
/// Configuration options for Keycloak OIDC authentication.
/// </summary>
public sealed class KeycloakOptions
{
    /// <summary>
    /// Section key in configuration. Sits under <c>Authentication:*</c> to match the
    /// ASP.NET convention used by <c>JwtBearerAuthOptions</c> (section
    /// <c>"Authentication"</c>) — both blocks live under the same parent so a host
    /// can see at a glance everything that affects token validation.
    /// </summary>
    public const string SectionName = "Authentication:Keycloak";

    /// <summary>OIDC authority URL (e.g. https://keycloak.example.com/realms/my-realm).</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>Keycloak client ID (e.g. my-backend).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret (confidential — load from Vault, never in plain text).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Require HTTPS for OIDC metadata. Default: true in production.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Expected audience in the token. Default: ClientId.</summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Source of roles in the Keycloak token.
    /// <list type="bullet">
    /// <item><c>"realm_access"</c> (default): realm roles (<c>realm_access.roles</c>)</item>
    /// <item><c>"resource_access"</c>: client roles (<c>resource_access.{ClientId}.roles</c>)</item>
    /// </list>
    /// </summary>
    public string RoleClaimsSource { get; set; } = "realm_access";

}
