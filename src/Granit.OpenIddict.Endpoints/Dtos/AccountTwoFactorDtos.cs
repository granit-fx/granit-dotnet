namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for 2FA status.
/// </summary>
/// <param name="IsEnabled">Whether 2FA is currently enabled.</param>
/// <param name="HasAuthenticatorApp">Whether an authenticator app is configured.</param>
/// <param name="RecoveryCodesLeft">Number of unused recovery codes remaining.</param>
public sealed record AccountTwoFactorStatusResponse(
    bool IsEnabled,
    bool HasAuthenticatorApp,
    int RecoveryCodesLeft);

/// <summary>
/// Response DTO for the authenticator key.
/// </summary>
/// <param name="SharedKey">The Base32-encoded shared key.</param>
/// <param name="QrCodeUri">The otpauth:// URI for QR code generation.</param>
public sealed record AccountAuthenticatorKeyResponse(
    string SharedKey,
    string QrCodeUri);

/// <summary>
/// Request DTO for enabling 2FA.
/// </summary>
/// <param name="Code">The 6-digit TOTP code to verify.</param>
public sealed record AccountTwoFactorEnableRequest(string Code);

/// <summary>
/// Response DTO after enabling 2FA with recovery codes.
/// </summary>
/// <param name="RecoveryCodes">The single-use recovery codes.</param>
public sealed record AccountTwoFactorEnableResponse(IReadOnlyList<string> RecoveryCodes);

/// <summary>
/// Response DTO for newly generated recovery codes.
/// </summary>
/// <param name="RecoveryCodes">The generated recovery codes.</param>
public sealed record AccountRecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);
