using Granit.Domain;

namespace Granit.Notifications.Domain;

/// <summary>
/// User notification preference: opt-in/opt-out per notification type and channel.
/// No preference = default from <see cref="NotificationDefinition.DefaultChannels"/>.
/// </summary>
public sealed class NotificationPreference : AuditedEntity, IMultiTenant
{
    public string UserId { get; set; } = string.Empty;
    public string NotificationTypeName { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public Guid? TenantId { get; set; }
}
