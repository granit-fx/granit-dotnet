using Granit.Domain;
using Granit.Notifications.Abstractions;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;

namespace Granit.Timeline.Notifications.Internal;

/// <summary>
/// Implements <see cref="ITimelineNotifier"/> by publishing notifications
/// via <see cref="INotificationPublisher"/>.
/// </summary>
internal sealed class NotificationBackedNotifier(
    INotificationPublisher publisher) : ITimelineNotifier
{
    private const int MaxBodyPreviewLength = 200;

    /// <inheritdoc/>
    public async Task NotifyEntryPostedAsync(
        TimelineEntry entry,
        IReadOnlyList<string> followerUserIds,
        CancellationToken cancellationToken = default)
    {
        if (followerUserIds.Count == 0)
        {
            return;
        }

        // Exclude the author from receiving their own notification
        var recipients = followerUserIds
            .Where(id => id != entry.AuthorId)
            .ToList();

        if (recipients.Count == 0)
        {
            return;
        }

        // Truncate body and use AuthorId only (AuthorName is PII / [SensitiveData]).
        TimelineCommentNotificationData data = new(
            entry.EntityType,
            entry.EntityId,
            entry.Id,
            entry.AuthorId,
            null,
            TruncateBody(entry.Body));

        EntityReference relatedEntity = new(entry.EntityType, entry.EntityId);

        await publisher.PublishAsync(
            TimelineCommentNotificationType.Instance,
            data,
            recipients,
            relatedEntity,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task NotifyMentionedUsersAsync(
        TimelineEntry entry,
        IReadOnlyList<string> mentionedUserIds,
        CancellationToken cancellationToken = default)
    {
        if (mentionedUserIds.Count == 0)
        {
            return;
        }

        // Exclude the author from receiving their own mention notification
        var recipients = mentionedUserIds
            .Where(id => id != entry.AuthorId)
            .ToList();

        if (recipients.Count == 0)
        {
            return;
        }

        // Truncate body and use AuthorId only.
        TimelineMentionNotificationData data = new(
            entry.EntityType,
            entry.EntityId,
            entry.Id,
            entry.AuthorId,
            null,
            TruncateBody(entry.Body));

        EntityReference relatedEntity = new(entry.EntityType, entry.EntityId);

        await publisher.PublishAsync(
            TimelineMentionNotificationType.Instance,
            data,
            recipients,
            relatedEntity,
            cancellationToken).ConfigureAwait(false);
    }

    private static string TruncateBody(string body)
    {
        if (body.Length <= MaxBodyPreviewLength)
        {
            return body;
        }

        return string.Concat(body.AsSpan(0, MaxBodyPreviewLength), "...");
    }
}
