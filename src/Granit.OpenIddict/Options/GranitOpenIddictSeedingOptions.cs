using Granit.Identity;
using Granit.MultiTenancy;

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
/// <param name="ConsentType">The consent type for the application (<c>"implicit"</c>, <c>"explicit"</c>, or <c>"systematic"</c>). Default: <c>"implicit"</c> (auto-grant for first-party apps).</param>
/// <param name="ApplicationType">The application type (<c>"web"</c> or <c>"native"</c>). Default: <c>"web"</c>.</param>
/// <param name="ClientSide">Optional host/tenant policy enforced at sign-in. <see cref="MultiTenancySides.Host"/> = only users with <c>TenantId = null</c> may obtain tokens for this client; <see cref="MultiTenancySides.Tenant"/> = only users with a non-null <c>TenantId</c>; <see cref="MultiTenancySides.Both"/> or <see langword="null"/> = no restriction. Stored on the OIDC application's <c>Properties</c> bag and enforced by <c>ClientSideAuthorizationHandler</c>.</param>
/// <param name="DeviceKind">Optional device classification for the devices that authenticate through this client (e.g. <see cref="DeviceKind.MobileApp"/>, <see cref="DeviceKind.Tv"/>). Stored on the OIDC application's <c>Properties</c> bag and read by the session adapters so <c>/devices</c> shows an accurate classification. <see langword="null"/> or <see cref="DeviceKind.Unknown"/> = not declared (the adapter falls back to a redirect-URI/grant heuristic).</param>
public sealed record OidcApplicationSeedDescriptor(
    string ClientId,
    string? ClientSecret,
    string DisplayName,
    string[] Permissions,
    string[] RedirectUris,
    string[] PostLogoutRedirectUris,
    string? SigningKeyJwk = null,
    string? ConsentType = null,
    string? ApplicationType = null,
    MultiTenancySides? ClientSide = null,
    DeviceKind? DeviceKind = null);

/// <summary>
/// Describes an OIDC scope to seed.
/// </summary>
/// <param name="Name">The scope name (unique key for upsert).</param>
/// <param name="DisplayName">A human-readable display name.</param>
/// <param name="Resources">Resources associated with this scope.</param>
/// <param name="Description">An optional human-readable description.</param>
public sealed record OidcScopeSeedDescriptor(
    string Name,
    string DisplayName,
    string[] Resources,
    string? Description = null);
