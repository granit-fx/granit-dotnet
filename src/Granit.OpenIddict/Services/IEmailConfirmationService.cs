namespace Granit.OpenIddict.Services;

/// <summary>
/// Sends and validates email confirmation tokens.
/// </summary>
public interface IEmailConfirmationService
{
    /// <summary>
    /// Sends a confirmation email to the specified user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="email">The email address to confirm.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendConfirmationEmailAsync(string userId, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the email confirmation token.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="token">The confirmation token from the email link.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the token is valid and the email is now confirmed.</returns>
    Task<bool> ConfirmAsync(string userId, string token, CancellationToken cancellationToken = default);
}
