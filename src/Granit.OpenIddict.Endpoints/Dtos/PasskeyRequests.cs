namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>Request DTO for passkey registration completion.</summary>
/// <param name="CredentialJson">The WebAuthn AuthenticatorAttestationResponse JSON.</param>
/// <param name="Name">Optional friendly name for the passkey.</param>
public sealed record PasskeyRegistrationRequest(string CredentialJson, string? Name);

/// <summary>Request DTO for passkey rename.</summary>
/// <param name="Name">The new friendly name (max 100 chars).</param>
public sealed record PasskeyRenameRequest(string Name);
