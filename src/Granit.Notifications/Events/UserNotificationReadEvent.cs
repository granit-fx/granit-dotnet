using Granit.Core.Events;

namespace Granit.Notifications.Events;

/// <summary>
/// Raised when a user notification transitions from Unread to Read.
/// Enables engagement tracking and read-receipt workflows.
/// </summary>
/// <param name="NotificationId">The unique identifier of the notification entry.</param>
/// <param name="RecipientUserId">Target user identifier.</param>
/// <param name="ReadAt">Timestamp when the notification was read.</param>
public sealed record UserNotificationReadEvent(
    Guid NotificationId,
    string RecipientUserId,
    DateTimeOffset ReadAt) : IDomainEvent;
