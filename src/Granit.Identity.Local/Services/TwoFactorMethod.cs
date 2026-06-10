namespace Granit.Identity.Local.Services;

/// <summary>
/// A second factor a user can complete the two-factor authentication challenge with.
/// </summary>
public enum TwoFactorMethod
{
    /// <summary>Time-based one-time password from an authenticator app (TOTP, RFC 6238).</summary>
    Authenticator,

    /// <summary>A single-use recovery code.</summary>
    RecoveryCode,

    /// <summary>A one-time code delivered to the user's confirmed email address.</summary>
    Email,
}
