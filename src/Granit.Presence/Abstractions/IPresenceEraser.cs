namespace Granit.Presence.Abstractions;

/// <summary>
/// GDPR Art. 17 / LGPD Art. 18 / CCPA erasure entry point for the presence module.
/// Removes both the persisted override and the cached heartbeat for the given user.
/// </summary>
/// <remarks>
/// <para>
/// Implementations MUST perform a hard delete (bypass soft-delete interceptors) so no
/// trace of the user's identifier remains in the presence table.
/// </para>
/// <para>
/// Wire this into the privacy deletion saga from the application — typically by
/// handling <c>PersonalDataDeletionRequestedEto</c> in a Wolverine handler that
/// resolves <see cref="IPresenceEraser"/> and calls <see cref="EraseAsync"/>. A future
/// <c>Granit.Presence.Privacy</c> package will ship that handler out of the box.
/// </para>
/// </remarks>
public interface IPresenceEraser
{
    /// <summary>
    /// Hard-deletes the user's presence row and removes their cached heartbeat.
    /// Idempotent — no-op when nothing exists.
    /// </summary>
    Task EraseAsync(Guid userId, CancellationToken cancellationToken);
}
