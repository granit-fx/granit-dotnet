namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO to complete registration started from an external provider whose data was
/// insufficient for a one-click sign-up (e.g. no email). The provider and provider key are NOT
/// accepted from the client — they travel inside the opaque, signed <paramref name="Token"/>
/// minted by the callback.
/// </summary>
/// <param name="Token">The opaque continuation token returned by the external-login callback.</param>
/// <param name="Email">The email address to register (must match the provider's verified email when present).</param>
/// <param name="FirstName">Optional first name; falls back to the provider value when omitted.</param>
/// <param name="LastName">Optional last name; falls back to the provider value when omitted.</param>
public sealed record RegisterExternalRequest(
    string Token,
    string Email,
    string? FirstName = null,
    string? LastName = null);
