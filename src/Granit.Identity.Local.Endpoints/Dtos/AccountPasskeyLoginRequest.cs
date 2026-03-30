namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request body for completing a WebAuthn passkey assertion (login).
/// </summary>
/// <param name="CredentialJson">
/// The serialized <c>AuthenticatorAssertionResponse</c> JSON from
/// <c>navigator.credentials.get()</c>.
/// </param>
public sealed record AccountPasskeyLoginRequest(string CredentialJson);
