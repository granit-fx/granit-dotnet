namespace Granit.Identity;

/// <summary>
/// Password management operations: query last change date, send reset email, set temporary password.
/// </summary>
public interface IIdentityPasswordManager
{
    /// <summary>Returns the timestamp of the user's last password change, or <c>null</c> if unknown.</summary>
    Task<DateTimeOffset?> GetPasswordChangedAtAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a password-reset email to the specified user.</summary>
    Task SendPasswordResetEmailAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Sets a temporary password for the specified user, forcing reset on next login.</summary>
    Task SetTemporaryPasswordAsync(
        string userId,
        string temporaryPassword,
        CancellationToken cancellationToken = default);
}
