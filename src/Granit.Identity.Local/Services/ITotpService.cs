namespace Granit.Identity.Local.Services;

/// <summary>
/// Provides TOTP (Time-based One-Time Password) operations for two-factor authentication.
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Generates a new shared secret key for TOTP setup.
    /// </summary>
    /// <returns>A Base32-encoded shared key.</returns>
    string GenerateSharedKey();

    /// <summary>
    /// Generates the provisioning URI for a QR code that can be scanned by an authenticator app.
    /// </summary>
    /// <param name="email">The user's email address (used as the account label).</param>
    /// <param name="sharedKey">The Base32-encoded shared key.</param>
    /// <returns>An <c>otpauth://</c> URI suitable for QR code generation.</returns>
    string GetQrCodeUri(string email, string sharedKey);

    /// <summary>
    /// Validates a TOTP code against the shared key.
    /// </summary>
    /// <param name="sharedKey">The Base32-encoded shared key.</param>
    /// <param name="code">The 6-digit TOTP code to validate.</param>
    /// <returns><see langword="true"/> if the code is valid within the current time window.</returns>
    bool ValidateCode(string sharedKey, string code);
}
