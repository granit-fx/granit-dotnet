using Granit.Core.Domain;

namespace Granit.Notifications.Domain;

/// <summary>
/// In-app notification stored in the user's inbox.
/// The database is the source of truth; email/SMS/push are derivative copies.
/// </summary>
public sealed class UserNotification : AggregateRoot, IMultiTenant
{
    // Parameterless constructor required by EF Core materializer.
    private UserNotification() { }

    /// <summary>
    /// Creates a new unread <see cref="UserNotification"/>.
    /// </summary>
    public static UserNotification Create(
        Guid id,
        Guid notificationId,
        string notificationTypeName,
        NotificationSeverity severity,
        string recipientUserId,
        System.Text.Json.JsonElement data,
        DateTimeOffset createdAt,
        Guid? tenantId = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null) => new()
        {
            Id = id,
            NotificationId = notificationId,
            NotificationTypeName = notificationTypeName,
            Severity = severity,
            RecipientUserId = recipientUserId,
            Data = data,
            State = UserNotificationState.Unread,
            CreatedAt = createdAt,
            TenantId = tenantId,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
        };

    public Guid NotificationId { get; private set; }
    public string NotificationTypeName { get; private set; } = string.Empty;
    public NotificationSeverity Severity { get; private set; }
    public string RecipientUserId { get; private set; } = string.Empty;
    public System.Text.Json.JsonElement Data { get; private set; }
    public UserNotificationState State { get; private set; } = UserNotificationState.Unread;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public Guid? TenantId { get; private set; }
    public string? RelatedEntityType { get; private set; }
    public string? RelatedEntityId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>
    /// Marks the notification as read.
    /// </summary>
    public void MarkAsRead(DateTimeOffset readAt)
    {
        if (State == UserNotificationState.Read)
        {
            return;
        }

        State = UserNotificationState.Read;
        ReadAt = readAt;
    }
}
