namespace Granit.Identity.Local.Services;

/// <summary>
/// Manages email change requests and confirmations.
/// </summary>
public interface IEmailChangeService
{
    /// <summary>
    /// Initiates an email change by generating a token and publishing
    /// <see cref="Events.EmailChangeRequestedEto"/>.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="newEmail">The requested new email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the request was initiated; <see langword="false"/> if the user was not found.</returns>
    Task<bool> RequestChangeAsync(string userId, string newEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms the email change using the token from the confirmation link.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="newEmail">The new email address encoded in the token.</param>
    /// <param name="token">The email change token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the email was changed successfully.</returns>
    Task<bool> ConfirmChangeAsync(string userId, string newEmail, string token, CancellationToken cancellationToken = default);
}
