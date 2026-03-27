using Granit.Notifications;

namespace Granit.Timeline.Notifications;

/// <summary>
/// Notification type for timeline comment/note postings.
/// </summary>
public sealed class TimelineCommentNotificationType
    : NotificationType<TimelineCommentNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly TimelineCommentNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "timeline.comment_posted";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.InApp, NotificationChannels.SignalR];
}

/// <summary>
/// Data payload for a timeline comment notification.
/// </summary>
/// <param name="EntityType">The entity type (e.g. "Patient").</param>
/// <param name="EntityId">The entity identifier.</param>
/// <param name="EntryId">The timeline entry identifier.</param>
/// <param name="AuthorId">The user who posted the comment.</param>
/// <param name="AuthorName">Display name of the author.</param>
/// <param name="Body">Comment body (Markdown).</param>
public sealed record TimelineCommentNotificationData(
    string EntityType,
    string EntityId,
    Guid EntryId,
    string AuthorId,
    string? AuthorName,
    string Body);
