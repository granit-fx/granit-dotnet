using Granit.Timeline.Domain;
using Granit.Timeline.Internal;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests.Internal;

public sealed class NullTimelineNotifierTests
{
    private readonly NullTimelineNotifier _notifier = new();

    [Fact]
    public async Task NotifyEntryPostedAsync_CompletesWithoutThrowing()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Patient", "123", TimelineEntryType.Comment,
            "Test comment", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");
        List<string> followerIds = ["user-2", "user-3"];

        Func<Task> act = () => _notifier.NotifyEntryPostedAsync(
            entry, followerIds, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task NotifyEntryPostedAsync_WithEmptyFollowers_CompletesWithoutThrowing()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Invoice", "456", TimelineEntryType.SystemLog,
            "{}", "system", "System", DateTimeOffset.UtcNow, "system");
        List<string> followerIds = [];

        Func<Task> act = () => _notifier.NotifyEntryPostedAsync(
            entry, followerIds, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task NotifyEntryPostedAsync_ReturnsCompletedTask()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Patient", "1", TimelineEntryType.Comment,
            "text", "user-1", "Bob", DateTimeOffset.UtcNow, "user-1");

        Task result = _notifier.NotifyEntryPostedAsync(entry, [], TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        await result;
    }

    [Fact]
    public async Task NotifyMentionedUsersAsync_CompletesWithoutThrowing()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Patient", "789", TimelineEntryType.Comment,
            "Hey @user-2", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");
        List<string> mentionedUserIds = ["user-2"];

        Func<Task> act = () => _notifier.NotifyMentionedUsersAsync(
            entry, mentionedUserIds, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task NotifyMentionedUsersAsync_WithEmptyMentions_CompletesWithoutThrowing()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Invoice", "1", TimelineEntryType.InternalNote,
            "No mentions here", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");
        List<string> mentionedUserIds = [];

        Func<Task> act = () => _notifier.NotifyMentionedUsersAsync(
            entry, mentionedUserIds, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task NotifyMentionedUsersAsync_ReturnsCompletedTask()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Patient", "1", TimelineEntryType.Comment,
            "text", "user-1", "Bob", DateTimeOffset.UtcNow, "user-1");

        Task result = _notifier.NotifyMentionedUsersAsync(entry, [], TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        await result;
    }
}
