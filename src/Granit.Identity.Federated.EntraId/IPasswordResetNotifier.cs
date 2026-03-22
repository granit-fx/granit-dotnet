namespace Granit.Identity.Federated.EntraId;

/// <summary>
/// Optional hook to notify a user after a temporary password has been set
/// via <see cref="IIdentityProvider.SendPasswordResetEmailAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Microsoft Entra ID does not natively support sending password reset emails via the Graph API.
/// When <see cref="IIdentityProvider.SendPasswordResetEmailAsync"/> is called, the Entra ID provider
/// generates a temporary password, sets it with <c>forceChangePasswordNextSignIn = true</c>,
/// then invokes this notifier to deliver the temporary password to the user.
/// </para>
/// <para>
/// Register an implementation (e.g. via <c>Granit.Notifications</c>) to send emails or other
/// notifications. If no implementation is registered, a warning is logged and the temporary
/// password is set silently.
/// </para>
/// </remarks>
public interface IPasswordResetNotifier
{
    /// <summary>
    /// Notifies a user that a temporary password has been set.
    /// </summary>
    /// <param name="userId">The user's Entra ID object ID.</param>
    /// <param name="temporaryPassword">The temporary password that was set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyAsync(string userId, string temporaryPassword, CancellationToken cancellationToken = default);
}
