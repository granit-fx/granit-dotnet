// =============================================================================
// Tests — NotificationBackedNotifier
// =============================================================================
// Verifies that the adapter correctly publishes notifications via
// INotificationPublisher and excludes the author from recipients.
// =============================================================================

using Granit.Domain;
using Granit.Notifications.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Domain.ValueObjects;
using Granit.Timeline.Notifications.Internal;
using NSubstitute;
using Xunit;

namespace Granit.Timeline.Notifications.Tests;

public sealed class NotificationBackedNotifierTests
{
    private readonly INotificationPublisher _publisher = Substitute.For<INotificationPublisher>();
    private readonly NotificationBackedNotifier _notifier;

    public NotificationBackedNotifierTests()
    {
        _notifier = new NotificationBackedNotifier(_publisher);
    }

    [Fact]
    public async Task NotifyEntryPostedAsync_PublishesCommentNotification()
    {
        TimelineEntry entry = BuildEntry();
        List<string> followers = ["user-1", "user-2", "author-1"];

        await _notifier.NotifyEntryPostedAsync(entry, followers, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            TimelineCommentNotificationType.Instance,
            Arg.Is<TimelineCommentNotificationData>(d =>
                d.EntityType == "Patient" &&
                d.EntityId == "p-1" &&
                d.AuthorId == "author-1"),
            Arg.Is<IReadOnlyList<string>>(r =>
                r.Count == 2 &&
                r.Contains("user-1") &&
                r.Contains("user-2")),
            Arg.Is<EntityReference>(e =>
                e.EntityType == "Patient" &&
                e.EntityId == "p-1"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NotifyEntryPostedAsync_ExcludesAuthor()
    {
        TimelineEntry entry = BuildEntry();
        List<string> followers = ["author-1"];

        await _notifier.NotifyEntryPostedAsync(entry, followers, TestContext.Current.CancellationToken);

        await _publisher.DidNotReceive().PublishAsync(
            Arg.Any<TimelineCommentNotificationType>(),
            Arg.Any<TimelineCommentNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<EntityReference?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyEntryPostedAsync_EmptyFollowers_DoesNotPublish()
    {
        TimelineEntry entry = BuildEntry();

        await _notifier.NotifyEntryPostedAsync(entry, [], TestContext.Current.CancellationToken);

        await _publisher.DidNotReceive().PublishAsync(
            Arg.Any<TimelineCommentNotificationType>(),
            Arg.Any<TimelineCommentNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<EntityReference?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyMentionedUsersAsync_PublishesMentionNotification()
    {
        TimelineEntry entry = BuildEntry();
        List<string> mentioned = ["user-3"];

        await _notifier.NotifyMentionedUsersAsync(entry, mentioned, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            TimelineMentionNotificationType.Instance,
            Arg.Is<TimelineMentionNotificationData>(d =>
                d.EntityType == "Patient" &&
                d.AuthorName == null),
            Arg.Is<IReadOnlyList<string>>(r =>
                r.Count == 1 &&
                r.Contains("user-3")),
            Arg.Is<EntityReference>(e =>
                e.EntityType == "Patient" &&
                e.EntityId == "p-1"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NotifyMentionedUsersAsync_ExcludesAuthor()
    {
        TimelineEntry entry = BuildEntry();
        List<string> mentioned = ["author-1"];

        await _notifier.NotifyMentionedUsersAsync(entry, mentioned, TestContext.Current.CancellationToken);

        await _publisher.DidNotReceive().PublishAsync(
            Arg.Any<TimelineMentionNotificationType>(),
            Arg.Any<TimelineMentionNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<EntityReference?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyReactionToggledAsync_PublishesReactionNotification()
    {
        TimelineEntry entry = BuildEntry();

        await _notifier.NotifyReactionToggledAsync(entry, "user-2", "👍", TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            TimelineReactionNotificationType.Instance,
            Arg.Is<TimelineReactionNotificationData>(d =>
                d.EntityType == "Patient" &&
                d.EntityId == "p-1" &&
                d.EntryId == entry.Id &&
                d.ReactingUserId == "user-2" &&
                d.ReactingUserName == null &&
                d.Emoji == "👍"),
            Arg.Is<IReadOnlyList<string>>(r =>
                r.Count == 1 &&
                r.Contains("author-1")),
            Arg.Is<EntityReference>(e =>
                e.EntityType == "Patient" &&
                e.EntityId == "p-1"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NotifyReactionToggledAsync_ReactorIsAuthor_DoesNotPublish()
    {
        TimelineEntry entry = BuildEntry();

        await _notifier.NotifyReactionToggledAsync(entry, "author-1", "👍", TestContext.Current.CancellationToken);

        await _publisher.DidNotReceive().PublishAsync(
            Arg.Any<TimelineReactionNotificationType>(),
            Arg.Any<TimelineReactionNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<EntityReference?>(),
            Arg.Any<CancellationToken>());
    }

    private static TimelineEntry BuildEntry() => TimelineEntry.Create(
        Guid.NewGuid(), new EntityReference("Patient", "p-1"), TimelineEntryType.Comment,
        "Hello @[User](user:00000000-0000-0000-0000-000000000001)",
        new AuthorInfo("author-1", "Author Name"), DateTimeOffset.UtcNow, "author-1");
}
