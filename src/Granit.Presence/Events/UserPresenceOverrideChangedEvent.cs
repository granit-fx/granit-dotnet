using Granit.Events;
using Granit.Presence.Domain;

namespace Granit.Presence.Events;

/// <summary>
/// Local domain event raised by <see cref="UserPresence"/> when a user changes their
/// manual override. Dispatched after <c>SaveChanges</c> commits.
/// </summary>
public sealed record UserPresenceOverrideChangedEvent(
    Guid UserId,
    ManualPresenceStatus FromStatus,
    ManualPresenceStatus ToStatus,
    DateTimeOffset? UntilUtc) : IDomainEvent;
