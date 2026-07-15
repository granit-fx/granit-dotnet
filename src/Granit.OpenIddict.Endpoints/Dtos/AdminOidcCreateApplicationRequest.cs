using Granit.Identity;
using Granit.MultiTenancy;

namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for creating a new OIDC application.
/// </summary>
/// <param name="ClientId">The client identifier (unique).</param>
/// <param name="DisplayName">A human-readable display name.</param>
/// <param name="ClientSecret">The client secret (null for public clients). Omit or set to null for public clients.</param>
/// <param name="Type">The application type (<c>web</c>, <c>native</c>). Default: <c>web</c>.</param>
/// <param name="Permissions">OpenIddict permissions to grant (e.g. <c>ept:token</c>, <c>gt:authorization_code</c>). Null or omitted defaults to no permissions.</param>
/// <param name="RedirectUris">Allowed redirect URIs. Null or omitted defaults to none.</param>
/// <param name="PostLogoutRedirectUris">Allowed post-logout redirect URIs. Null or omitted defaults to none.</param>
/// <param name="ConsentType">The consent type (<c>implicit</c>, <c>explicit</c>, or <c>systematic</c>). Default: <c>implicit</c>.</param>
/// <param name="SigningKeyJwk">Public signing key as JWK JSON for <c>private_key_jwt</c> authentication (RFC 7523). Null for shared-secret clients.</param>
/// <param name="ClientSide">Host/tenant policy enforced at sign-in. Null means no restriction.</param>
/// <param name="DeviceKind">Device classification for the devices that authenticate through this client (e.g. <c>MobileApp</c>, <c>Tv</c>). Null or omitted means not declared (the session adapters fall back to a redirect-URI/grant heuristic).</param>
/// <param name="TenantId">Owning tenant. Omit (or <see langword="null"/>) to create a global application, or to inherit the caller's active tenant when the admin API is invoked under a tenant scope. A host administrator (no active tenant) may set this explicitly to provision an application for a specific tenant; a tenant-scoped administrator may only target their own tenant (a mismatch is rejected with 403).</param>
#pragma warning disable GRSEC003 // ClientSecret is a DTO parameter, not a stored secret
public sealed record AdminOidcCreateApplicationRequest(
    string ClientId,
    string? DisplayName = null,
    string? ClientSecret = null,
    string? Type = null,
    string[]? Permissions = null,
    string[]? RedirectUris = null,
    string[]? PostLogoutRedirectUris = null,
    string? ConsentType = null,
    string? SigningKeyJwk = null,
    MultiTenancySides? ClientSide = null,
    DeviceKind? DeviceKind = null,
    Guid? TenantId = null);
#pragma warning restore GRSEC003
