using Granit.Timeline.Domain;

namespace Granit.Timeline.Abstractions;

/// <summary>
/// Sends notifications to entity followers when a timeline event occurs.
/// When <c>Granit.Notifications</c> is not available, this is a no-op implementation.
/// </summary>
public interface ITimelineNotifier
{
    /// <summary>Notifies followers that a new comment or note was posted.</summary>
    Task NotifyEntryPostedAsync(
        TimelineEntry entry,
        IReadOnlyList<string> followerUserIds,
        CancellationToken cancellationToken = default);

    /// <summary>Notifies mentioned users and auto-subscribes them.</summary>
    Task NotifyMentionedUsersAsync(
        TimelineEntry entry,
        IReadOnlyList<string> mentionedUserIds,
        CancellationToken cancellationToken = default);

    /// <summary>Notifies the entry's author that someone reacted to it. No-op if the reactor is the author.</summary>
    Task NotifyReactionToggledAsync(
        TimelineEntry entry,
        string reactingUserId,
        string emoji,
        CancellationToken cancellationToken = default);
}
