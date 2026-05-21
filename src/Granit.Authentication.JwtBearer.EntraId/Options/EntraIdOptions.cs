namespace Granit.Authentication.JwtBearer.EntraId.Options;

/// <summary>
/// Configuration options for Microsoft Entra ID (Azure AD) OIDC authentication.
/// </summary>
public sealed class EntraIdOptions
{
    /// <summary>Section key in the configuration.</summary>
    public const string SectionName = "Authentication:EntraId";

    /// <summary>
    /// Azure AD instance URL. Default: <c>https://login.microsoftonline.com/</c>.
    /// </summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>Azure AD tenant ID (e.g. <c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>).</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>App registration client ID (audience for the JWT token).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Require HTTPS for OIDC metadata. Default: true in production.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Computed OIDC v2.0 authority URL.
    /// </summary>
    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";
}
