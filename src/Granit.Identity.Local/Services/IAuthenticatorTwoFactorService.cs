namespace Granit.Identity.Local.Services;

/// <summary>
/// Manages enrollment of the TOTP authenticator-app factor (RFC 6238).
/// </summary>
/// <remarks>
/// Factor-agnostic concerns (aggregate status, recovery codes, the master kill switch)
/// live on <see cref="ITwoFactorService"/>.
/// </remarks>
public interface IAuthenticatorTwoFactorService
{
    /// <summary>
    /// Returns the shared key and QR code URI for authenticator setup, generating a
    /// new key when none exists yet.
    /// </summary>
    Task<AuthenticatorKeyInfo> GetKeyAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables the authenticator factor after verifying the TOTP code, turning on the
    /// master switch. Returns freshly generated recovery codes.
    /// </summary>
    Task<IReadOnlyList<string>> EnableAsync(string userId, string code, CancellationToken cancellationToken = default);

    /// <summary>Resets the authenticator key (invalidates the existing authenticator).</summary>
    Task ResetAsync(string userId, CancellationToken cancellationToken = default);
}
