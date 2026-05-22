using Granit.Presence.Domain;

namespace Granit.Presence.Abstractions;

/// <summary>
/// Application service that mutates a user's manual override and emits change events.
/// </summary>
public interface IPresenceOverrideService
{
    /// <summary>
    /// Sets or clears the user's manual override. Passing
    /// <see cref="ManualPresenceStatus.Available"/> clears any existing override.
    /// </summary>
    Task<PresenceSnapshot> SetAsync(
        Guid userId,
        ManualPresenceStatus status,
        DateTimeOffset? untilUtc,
        CancellationToken cancellationToken);

    /// <summary>Clears the user's manual override.</summary>
    Task<PresenceSnapshot> ClearAsync(Guid userId, CancellationToken cancellationToken);
}
