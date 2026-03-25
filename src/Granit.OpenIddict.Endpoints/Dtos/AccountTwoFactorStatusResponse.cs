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
