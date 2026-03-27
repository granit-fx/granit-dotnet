namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for secret rotation. Contains the new plaintext secret (shown once).
/// </summary>
/// <param name="ClientId">The client identifier.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="NewClientSecret">The new plaintext secret (displayed once, never stored in plaintext).</param>
#pragma warning disable GRSEC003 // NewClientSecret is returned once to the admin, not stored
public sealed record AdminOidcRotateSecretResponse(
    string? ClientId,
    string? DisplayName,
    string NewClientSecret);
#pragma warning restore GRSEC003
