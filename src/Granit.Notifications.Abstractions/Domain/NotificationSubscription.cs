using Granit.Domain;

namespace Granit.Notifications.Domain;

/// <summary>
/// Notification subscription: either a topic subscription (EntityType/EntityId null)
/// or an entity follower.
/// </summary>
public sealed class NotificationSubscription : CreationAuditedEntity, IMultiTenant
{
    public string UserId { get; set; } = string.Empty;
    public string NotificationTypeName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
}
