using Granit.Core.Events;

namespace Granit.Notifications.Events;

/// <summary>
/// Raised after a <see cref="Domain.UserNotification"/> is persisted in the user's inbox.
/// </summary>
public sealed record UserNotificationCreatedEvent(
    Guid NotificationId,
    string NotificationTypeName,
    NotificationSeverity Severity,
    string RecipientUserId,
    Guid? TenantId) : IDomainEvent;
