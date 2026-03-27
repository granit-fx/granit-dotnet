namespace Granit.Identity.Local.Services;

/// <summary>
/// Generates password reset tokens and publishes <see cref="Granit.Identity.Local.Events.PasswordResetRequestedEto"/>.
/// </summary>
/// <remarks>
/// The service does NOT send emails. It publishes an integration event that
/// a subscriber (Granit.Notifications or app-level) consumes to send the reset email.
/// </remarks>
public interface IPasswordResetService
{
    /// <summary>
    /// Generates a reset token for the user and publishes <see cref="Granit.Identity.Local.Events.PasswordResetRequestedEto"/>.
    /// </summary>
    /// <param name="email">The email address of the account to reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the user exists and the event was published; <see langword="false"/> if the user was not found (caller should still return 202).</returns>
    Task<bool> RequestResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the reset token and sets the new password.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="token">The password reset token.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken cancellationToken = default);
}
