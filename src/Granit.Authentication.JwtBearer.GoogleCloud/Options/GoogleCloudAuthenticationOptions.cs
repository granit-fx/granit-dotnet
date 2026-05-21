namespace Granit.Authentication.JwtBearer.GoogleCloud.Options;

/// <summary>
/// Configuration options for Google Cloud Identity Platform (Firebase Auth) OIDC authentication.
/// </summary>
public sealed class GoogleCloudAuthenticationOptions
{
    /// <summary>Section key in the configuration.</summary>
    public const string SectionName = "Authentication:GoogleCloud";

    /// <summary>
    /// GCP project ID. Used to derive the OIDC authority
    /// (<c>https://securetoken.google.com/{ProjectId}</c>).
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Require HTTPS for OIDC metadata. Default: true in production.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Custom claims key that contains roles (e.g. <c>"roles"</c>).
    /// Firebase Auth stores roles in custom claims as a JSON array.
    /// Default: <c>"roles"</c>.
    /// </summary>
    public string RolesClaimKey { get; set; } = "roles";

    /// <summary>
    /// Computed OIDC authority URL for Firebase Auth tokens.
    /// </summary>
    public string Authority => $"https://securetoken.google.com/{ProjectId}";
}
