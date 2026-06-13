using Granit.Identity;
using Granit.MultiTenancy;

namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for updating an existing OIDC application.
/// </summary>
/// <remarks>
/// All fields are optional. Null values leave the corresponding field unchanged.
/// To clear a collection, pass an empty array. To clear the signing key, pass an empty string.
/// </remarks>
/// <param name="DisplayName">A human-readable display name. Null leaves it unchanged.</param>
/// <param name="Type">The application type (<c>web</c>, <c>native</c>). Null leaves it unchanged.</param>
/// <param name="Permissions">Replaces the full permission set. Null leaves it unchanged.</param>
/// <param name="RedirectUris">Replaces the full redirect URI list. Null leaves it unchanged.</param>
/// <param name="PostLogoutRedirectUris">Replaces the full post-logout redirect URI list. Null leaves it unchanged.</param>
/// <param name="ConsentType">The consent type (<c>implicit</c>, <c>explicit</c>, or <c>systematic</c>). Null leaves it unchanged.</param>
/// <param name="SigningKeyJwk">Public signing key as JWK JSON for <c>private_key_jwt</c> authentication. Null leaves it unchanged; empty string clears the key.</param>
/// <param name="ClientSide">Host/tenant policy enforced at sign-in. Null leaves it unchanged.</param>
/// <param name="DeviceKind">Device classification for the devices that authenticate through this client. Null leaves it unchanged; <c>Unknown</c> clears the declaration.</param>
public sealed record AdminOidcUpdateApplicationRequest(
    string? DisplayName = null,
    string? Type = null,
    string[]? Permissions = null,
    string[]? RedirectUris = null,
    string[]? PostLogoutRedirectUris = null,
    string? ConsentType = null,
    string? SigningKeyJwk = null,
    MultiTenancySides? ClientSide = null,
    DeviceKind? DeviceKind = null);
