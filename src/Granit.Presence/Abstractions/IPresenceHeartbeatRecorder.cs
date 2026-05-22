using Granit.Presence.Domain;

namespace Granit.Presence.Abstractions;

/// <summary>
/// Coordinates a heartbeat: records the poll into the tracker, recomputes the user's
/// effective status, and publishes <see cref="Events.UserPresenceChangedEto"/> if it
/// has transitioned since the last call.
/// </summary>
public interface IPresenceHeartbeatRecorder
{
    /// <summary>
    /// Records a heartbeat and returns the post-heartbeat snapshot.
    /// </summary>
    /// <param name="userId">The user reporting activity.</param>
    /// <param name="idleDuration">Client-reported idle duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's snapshot after the heartbeat is applied.</returns>
    Task<PresenceSnapshot> RecordAsync(
        Guid userId,
        TimeSpan idleDuration,
        CancellationToken cancellationToken);
}
