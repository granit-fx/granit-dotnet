using Granit.Notifications;

namespace Granit.Timeline.Notifications;

/// <summary>
/// Notification type for @mention in a timeline entry.
/// </summary>
public sealed class TimelineMentionNotificationType
    : NotificationType<TimelineMentionNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly TimelineMentionNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "timeline.user_mentioned";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.InApp, NotificationChannels.SignalR, NotificationChannels.Email];
}

/// <summary>
/// Data payload for a timeline @mention notification.
/// </summary>
/// <param name="EntityType">The entity type (e.g. "Patient").</param>
/// <param name="EntityId">The entity identifier.</param>
/// <param name="EntryId">The timeline entry identifier.</param>
/// <param name="AuthorId">The user who mentioned.</param>
/// <param name="AuthorName">Display name of the author.</param>
/// <param name="Body">Entry body containing the mention.</param>
public sealed record TimelineMentionNotificationData(
    string EntityType,
    string EntityId,
    Guid EntryId,
    string AuthorId,
    string? AuthorName,
    string Body);
