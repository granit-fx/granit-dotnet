namespace Granit.OpenIddict.Options;

/// <summary>
/// Options for declarative OIDC application and scope seeding.
/// </summary>
public sealed class GranitOpenIddictSeedingOptions
{
    /// <summary>Configuration section name for binding from <c>appsettings.json</c>.</summary>
    public const string SectionName = "OpenIddict:Seeding";

    /// <summary>
    /// Gets or sets the OIDC applications to seed at startup.
    /// </summary>
    public OidcApplicationSeedDescriptor[] Applications { get; set; } = [];

    /// <summary>
    /// Gets or sets the OIDC scopes to seed at startup.
    /// </summary>
    public OidcScopeSeedDescriptor[] Scopes { get; set; } = [];
}

/// <summary>
/// Describes an OIDC application to seed.
/// </summary>
/// <param name="ClientId">The client identifier (unique key for upsert).</param>
/// <param name="ClientSecret">The client secret (encrypted before persistence). Null for public clients or <c>private_key_jwt</c> clients. Use Vault in production.</param>
/// <param name="DisplayName">A human-readable display name.</param>
/// <param name="Permissions">OpenIddict permissions (e.g., <c>ept:token</c>, <c>gt:authorization_code</c>).</param>
/// <param name="RedirectUris">Allowed redirect URIs.</param>
/// <param name="PostLogoutRedirectUris">Allowed post-logout redirect URIs.</param>
/// <param name="SigningKeyJwk">Optional public signing key as JWK JSON for <c>private_key_jwt</c> client authentication (RFC 7523). When set, the client authenticates with a signed JWT assertion instead of a shared secret.</param>
public sealed record OidcApplicationSeedDescriptor(
    string ClientId,
    string? ClientSecret,
    string DisplayName,
    string[] Permissions,
    string[] RedirectUris,
    string[] PostLogoutRedirectUris,
    string? SigningKeyJwk = null);

/// <summary>
/// Describes an OIDC scope to seed.
/// </summary>
/// <param name="Name">The scope name (unique key for upsert).</param>
/// <param name="DisplayName">A human-readable display name.</param>
/// <param name="Resources">Resources associated with this scope.</param>
public sealed record OidcScopeSeedDescriptor(
    string Name,
    string DisplayName,
    string[] Resources);
