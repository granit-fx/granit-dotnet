namespace Granit.Bff.Options;

/// <summary>
/// Configuration options for the BFF security proxy.
/// Bind from <c>Bff</c> configuration section.
/// </summary>
public sealed class GranitBffOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Bff";

    /// <summary>OIDC authority URL (e.g., <c>https://auth.example.com</c>).</summary>
    public Uri Authority { get; set; } = null!;

    /// <summary>Client ID for the BFF (confidential client).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret for the BFF (confidential client).</summary>
#pragma warning disable GRSEC003 // Property name contains "Secret" — required configuration
    public string ClientSecret { get; set; } = string.Empty;
#pragma warning restore GRSEC003

    /// <summary>Scopes to request (default: openid profile email roles offline_access).</summary>
    public string[] Scopes { get; set; } = ["openid", "profile", "email", "roles", "offline_access"];

    /// <summary>Session cookie name (default: <c>__Host-granit-bff</c>).</summary>
    public string SessionCookieName { get; set; } = "__Host-granit-bff";

    /// <summary>Session duration (default: 8 hours). After this, user must re-authenticate.</summary>
    public TimeSpan SessionDuration { get; set; } = TimeSpan.FromHours(8);

    /// <summary>Token refresh grace period (default: 1 minute before expiry).</summary>
    public TimeSpan RefreshGracePeriod { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Post-login redirect path (default: <c>/</c>).</summary>
    public string PostLoginRedirectPath { get; set; } = "/";

    /// <summary>Post-logout redirect path (default: <c>/</c>).</summary>
    public string PostLogoutRedirectPath { get; set; } = "/";
}
