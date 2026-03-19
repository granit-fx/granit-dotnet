using Granit.Core.Events;

namespace Granit.Notifications.Events;

/// <summary>
/// Raised when a <see cref="Domain.UserNotification"/> is marked as read by the recipient.
/// </summary>
public sealed record UserNotificationReadEvent(
    Guid NotificationId,
    string RecipientUserId,
    DateTimeOffset ReadAt) : IDomainEvent;
