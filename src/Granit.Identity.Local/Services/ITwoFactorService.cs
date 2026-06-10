namespace Granit.Identity.Local.Services;

/// <summary>
/// Coordinates cross-cutting two-factor authentication state that is not bound to a
/// single factor: aggregate status, the available-methods list, recovery codes, and
/// the master kill switch.
/// </summary>
/// <remarks>
/// <para>
/// Per-factor enrollment lives in <see cref="IAuthenticatorTwoFactorService"/> (TOTP)
/// and <see cref="IEmailTwoFactorService"/> (email OTP). Recovery codes are a
/// factor-agnostic fallback and therefore belong here rather than to any single factor.
/// </para>
/// <para>
/// Verification at login is handled by ASP.NET Core Identity's <c>SignInManager</c>
/// directly (its token providers are polymorphic over the method), so this abstraction
/// intentionally exposes no verify method.
/// </para>
/// </remarks>
public interface ITwoFactorService
{
    /// <summary>Returns the aggregate 2FA status for a user across all factors.</summary>
    Task<TwoFactorStatus> GetStatusAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the factors the user can complete the challenge with, given their
    /// current enrollment. Empty when 2FA is not enabled.
    /// </summary>
    Task<IReadOnlyList<TwoFactorMethod>> GetAvailableMethodsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Generates new single-use recovery codes (invalidates previous ones).</summary>
    Task<IReadOnlyList<string>> GenerateRecoveryCodesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables every two-factor method for the user (master kill switch): clears the
    /// authenticator key, removes the email factor, turns off the master switch and
    /// rotates the security stamp to force re-authentication.
    /// </summary>
    Task DisableAllAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>Current 2FA status for a user.</summary>
/// <param name="IsEnabled">Whether 2FA is currently enabled (any factor active).</param>
/// <param name="HasAuthenticatorApp">Whether an authenticator key is set.</param>
/// <param name="HasEmailOtp">Whether the email one-time-code factor is enrolled.</param>
/// <param name="RecoveryCodesLeft">Number of unused recovery codes remaining.</param>
public sealed record TwoFactorStatus(
    bool IsEnabled,
    bool HasAuthenticatorApp,
    bool HasEmailOtp,
    int RecoveryCodesLeft);

/// <summary>Authenticator key information for QR code generation.</summary>
/// <param name="SharedKey">The Base32-encoded shared key.</param>
/// <param name="QrCodeUri">The otpauth:// URI for authenticator app scanning.</param>
#pragma warning disable GRSEC003 // Authenticator key properties, not secrets
public sealed record AuthenticatorKeyInfo(string SharedKey, string QrCodeUri);
#pragma warning restore GRSEC003
