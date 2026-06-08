using Granit.MultiTenancy;

namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for OIDC application administration endpoints.
/// </summary>
/// <param name="ClientId">The client identifier.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Type">The application type (confidential, public).</param>
/// <param name="TenantId">The tenant identifier, or <see langword="null"/> for global applications.</param>
/// <param name="Permissions">The OpenIddict permissions granted to this client (e.g. <c>ept:token</c>, <c>gt:authorization_code</c>).</param>
/// <param name="RedirectUris">The allowed redirect URIs.</param>
/// <param name="PostLogoutRedirectUris">The allowed post-logout redirect URIs.</param>
/// <param name="ConsentType">The consent type (<c>implicit</c>, <c>explicit</c>, or <c>systematic</c>).</param>
/// <param name="ClientSide">The host/tenant policy enforced at sign-in, or <see langword="null"/> for no restriction.</param>
/// <param name="HasSigningKey">Whether a public signing key (JWK) is registered for <c>private_key_jwt</c> authentication.</param>
public sealed record AdminOidcApplicationResponse(
    string? ClientId,
    string? DisplayName,
    string? Type,
    Guid? TenantId,
    string[] Permissions,
    string[] RedirectUris,
    string[] PostLogoutRedirectUris,
    string? ConsentType,
    MultiTenancySides? ClientSide,
    bool HasSigningKey);
