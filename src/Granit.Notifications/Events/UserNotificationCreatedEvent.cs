using Granit.Core.Events;
using Granit.Notifications.Domain;

namespace Granit.Notifications.Events;

/// <summary>
/// Raised when a new user notification is created in the inbox.
/// Enables in-process delivery pipelines (email/SMS/push).
/// </summary>
/// <param name="NotificationId">Global notification identifier.</param>
/// <param name="NotificationTypeName">Logical type (e.g., <c>"order.created"</c>).</param>
/// <param name="Severity">Notification severity level.</param>
/// <param name="RecipientUserId">Target user identifier.</param>
/// <param name="TenantId">Tenant scope. <c>null</c> for host-level notifications.</param>
public sealed record UserNotificationCreatedEvent(
    Guid NotificationId,
    string NotificationTypeName,
    NotificationSeverity Severity,
    string RecipientUserId,
    Guid? TenantId) : IDomainEvent;
