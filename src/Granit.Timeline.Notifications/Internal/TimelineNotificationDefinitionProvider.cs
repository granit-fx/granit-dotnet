using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Timeline.Notifications.Internal;

/// <summary>
/// Registers timeline notification definitions. Both entries are
/// user-facing collaboration features — opt-out is allowed.
/// </summary>
internal sealed class TimelineNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Collaboration";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(TimelineMentionNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Mentioned in Timeline",
            Description = "Notifies a user when they are @-mentioned in a timeline entry.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.InApp, NotificationChannels.SignalR, NotificationChannels.Email],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(TimelineCommentNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Timeline Comment",
            Description = "Notifies timeline followers when a new comment is posted on an entity they follow.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.InApp, NotificationChannels.SignalR],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(TimelineReactionNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Timeline Reaction",
            Description = "Notifies the entry's author when someone reacts to their timeline entry.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.InApp, NotificationChannels.SignalR],
            AllowUserOptOut = true,
        });
    }
}
