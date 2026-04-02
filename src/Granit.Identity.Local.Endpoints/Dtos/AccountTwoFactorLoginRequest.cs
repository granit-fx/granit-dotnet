namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request body for completing two-factor authentication during login.
/// </summary>
/// <param name="Code">The 6-digit TOTP code or a single-use recovery code.</param>
/// <param name="UseRecoveryCode">
/// When <see langword="true"/>, <paramref name="Code"/> is treated as a recovery code
/// instead of a TOTP code. Default: <see langword="false"/>.
/// </param>
/// <param name="RememberMe">
/// When <see langword="true"/>, the Identity session cookie is marked as persistent.
/// Default: <see langword="false"/>.
/// </param>
public sealed record AccountTwoFactorLoginRequest(
    string Code,
    bool UseRecoveryCode = false,
    bool RememberMe = false);
