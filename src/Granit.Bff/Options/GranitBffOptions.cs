namespace Granit.Bff.Options;

/// <summary>
/// Configuration options for the BFF security proxy.
/// Bind from <c>Bff</c> configuration section.
/// </summary>
/// <remarks>
/// Supports multiple frontends served from a single backend. Each frontend
/// has its own OIDC client, session cookie, scopes, and path prefix.
/// </remarks>
public sealed class GranitBffOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Bff";

    /// <summary>OIDC authority URL (e.g., <c>https://auth.example.com</c>).</summary>
    public Uri Authority { get; set; } = null!;

    /// <summary>Session duration (default: 8 hours). After this, user must re-authenticate.</summary>
    public TimeSpan SessionDuration { get; set; } = TimeSpan.FromHours(8);

    /// <summary>Token refresh grace period (default: 1 minute before expiry).</summary>
    public TimeSpan RefreshGracePeriod { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Registered frontend applications. Each frontend has its own OIDC client,
    /// session cookie, and path prefix.
    /// </summary>
    /// <remarks>
    /// Example: Admin, Patient, Médecin, Mutuelle — each with different scopes
    /// and permissions but sharing the same backend API and identity provider.
    /// </remarks>
    public List<BffFrontendOptions> Frontends { get; set; } = [];
}

/// <summary>
/// Configuration for a single frontend application served by the BFF.
/// </summary>
#pragma warning disable GRSEC003 // Property name contains "Secret" — required configuration
public sealed class BffFrontendOptions
{
    /// <summary>Unique name for this frontend (e.g., <c>"admin"</c>, <c>"patient"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>OIDC client ID (confidential client).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OIDC client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>OIDC scopes to request.</summary>
    public string[] Scopes { get; set; } = ["openid", "profile", "email", "roles", "offline_access"];

    /// <summary>
    /// URL path prefix for this frontend (e.g., <c>"/admin"</c>, <c>"/patient"</c>).
    /// </summary>
    /// <remarks>
    /// BFF endpoints are mounted under this prefix:
    /// <c>/{PathPrefix}/bff/login</c>, <c>/{PathPrefix}/bff/user</c>, etc.
    /// </remarks>
    public string PathPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Path to the SPA static files for this frontend (e.g., <c>"wwwroot/admin"</c>).
    /// </summary>
    public string StaticFilesPath { get; set; } = string.Empty;

    /// <summary>Post-login redirect path (default: the frontend's path prefix + <c>"/"</c>).</summary>
    public string? PostLoginRedirectPath { get; set; }

    /// <summary>Post-logout redirect path (default: the frontend's path prefix + <c>"/"</c>).</summary>
    public string? PostLogoutRedirectPath { get; set; }

    /// <summary>
    /// Gets the session cookie name for this frontend.
    /// Format: <c>__Host-granit-bff-{name}</c>.
    /// </summary>
    public string SessionCookieName => $"__Host-granit-bff-{Name}";

    /// <summary>
    /// Gets the effective post-login redirect path.
    /// </summary>
    public string EffectivePostLoginRedirectPath =>
        PostLoginRedirectPath ?? (string.IsNullOrEmpty(PathPrefix) ? "/" : $"{PathPrefix}/");

    /// <summary>
    /// Gets the effective post-logout redirect path.
    /// </summary>
    public string EffectivePostLogoutRedirectPath =>
        PostLogoutRedirectPath ?? (string.IsNullOrEmpty(PathPrefix) ? "/" : $"{PathPrefix}/");

    /// <summary>
    /// Gets or sets a value indicating whether the BFF should use Pushed Authorization
    /// Requests (RFC 9126) when initiating the OIDC login flow.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the BFF posts authorization parameters to the
    /// <c>/connect/par</c> endpoint and redirects the user with only the
    /// <c>request_uri</c>. Requires the OIDC server to support PAR.
    /// Default: <see langword="false"/>.
    /// </remarks>
    public bool UsePushedAuthorizationRequests { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the BFF should use DPoP
    /// (Demonstrating Proof-of-Possession, RFC 9449) to bind tokens to a
    /// cryptographic key, preventing token replay attacks.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the BFF generates an EC P-256 key pair per session,
    /// includes DPoP proofs in token requests and proxied API calls, and uses the
    /// <c>DPoP</c> token scheme instead of <c>Bearer</c>.
    /// Default: <see langword="false"/>.
    /// </remarks>
    public bool UseDPoP { get; set; }
}
#pragma warning restore GRSEC003
