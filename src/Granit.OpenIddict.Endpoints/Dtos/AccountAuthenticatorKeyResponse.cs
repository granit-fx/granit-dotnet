namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for the authenticator key.
/// </summary>
/// <param name="SharedKey">The Base32-encoded shared key.</param>
/// <param name="QrCodeUri">The otpauth:// URI for QR code generation.</param>
public sealed record AccountAuthenticatorKeyResponse(
    string SharedKey,
    string QrCodeUri);
