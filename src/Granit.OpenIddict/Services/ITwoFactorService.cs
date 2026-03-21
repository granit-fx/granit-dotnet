namespace Granit.OpenIddict.Services;

/// <summary>
/// High-level service for managing TOTP two-factor authentication state.
/// </summary>
/// <remarks>
/// Wraps <c>UserManager&lt;GranitUser&gt;</c> 2FA operations behind an abstraction
/// consumable by the endpoints layer without a direct EF Core dependency.
/// </remarks>
public interface ITwoFactorService
{
    /// <summary>Returns the current 2FA status for a user.</summary>
    Task<TwoFactorStatus> GetStatusAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the shared key and QR code URI for authenticator setup.</summary>
    Task<AuthenticatorKeyInfo> GetAuthenticatorKeyAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Enables 2FA after verifying the TOTP code. Returns recovery codes.</summary>
    Task<IReadOnlyList<string>> EnableAsync(string userId, string code, CancellationToken cancellationToken = default);

    /// <summary>Disables 2FA for a user.</summary>
    Task DisableAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Resets the authenticator key (invalidates existing authenticator).</summary>
    Task ResetAuthenticatorAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Generates new single-use recovery codes (invalidates previous ones).</summary>
    Task<IReadOnlyList<string>> GenerateRecoveryCodesAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>Current 2FA status for a user.</summary>
/// <param name="IsEnabled">Whether 2FA is currently enabled.</param>
/// <param name="HasAuthenticatorApp">Whether an authenticator key is set.</param>
/// <param name="RecoveryCodesLeft">Number of unused recovery codes remaining.</param>
public sealed record TwoFactorStatus(bool IsEnabled, bool HasAuthenticatorApp, int RecoveryCodesLeft);

/// <summary>Authenticator key information for QR code generation.</summary>
/// <param name="SharedKey">The Base32-encoded shared key.</param>
/// <param name="QrCodeUri">The otpauth:// URI for authenticator app scanning.</param>
#pragma warning disable GRSEC003 // Authenticator key properties, not secrets
public sealed record AuthenticatorKeyInfo(string SharedKey, string QrCodeUri);
#pragma warning restore GRSEC003
