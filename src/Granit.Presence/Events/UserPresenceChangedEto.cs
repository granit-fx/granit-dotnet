using Granit.Events;
using Granit.Presence.Domain;

namespace Granit.Presence.Events;

/// <summary>
/// Distributed integration event raised when a user's effective presence status changes.
/// Consumers may use this to invalidate cached views or fan out to connected clients.
/// </summary>
public sealed record UserPresenceChangedEto(
    Guid UserId,
    PresenceStatus FromStatus,
    PresenceStatus ToStatus,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent;
