using Granit.Domain;
using Granit.Timeline.Domain;
using Granit.Timeline.Domain.ValueObjects;
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
            Guid.NewGuid(), new EntityReference("Patient", "123"), TimelineEntryType.Comment,
            "Test comment", new AuthorInfo("user-1", "Alice"), DateTimeOffset.UtcNow, "user-1");
        List<string> followerIds = ["user-2", "user-3"];

        Task act() => _notifier.NotifyEntryPostedAsync(
            entry, followerIds, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task NotifyEntryPostedAsync_WithEmptyFollowers_CompletesWithoutThrowing()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), new EntityReference("Invoice", "456"), TimelineEntryType.SystemLog,
            "{}", new AuthorInfo("system", "System"), DateTimeOffset.UtcNow, "system");
        List<string> followerIds = [];

        Task act() => _notifier.NotifyEntryPostedAsync(
            entry, followerIds, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task NotifyEntryPostedAsync_ReturnsCompletedTask()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), new EntityReference("Patient", "1"), TimelineEntryType.Comment,
            "text", new AuthorInfo("user-1", "Bob"), DateTimeOffset.UtcNow, "user-1");

        Task result = _notifier.NotifyEntryPostedAsync(entry, [], TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        await result;
    }

    [Fact]
    public async Task NotifyMentionedUsersAsync_CompletesWithoutThrowing()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), new EntityReference("Patient", "789"), TimelineEntryType.Comment,
            "Hey @user-2", new AuthorInfo("user-1", "Alice"), DateTimeOffset.UtcNow, "user-1");
        List<string> mentionedUserIds = ["user-2"];

        Task act() => _notifier.NotifyMentionedUsersAsync(
            entry, mentionedUserIds, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task NotifyMentionedUsersAsync_WithEmptyMentions_CompletesWithoutThrowing()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), new EntityReference("Invoice", "1"), TimelineEntryType.InternalNote,
            "No mentions here", new AuthorInfo("user-1", "Alice"), DateTimeOffset.UtcNow, "user-1");
        List<string> mentionedUserIds = [];

        Task act() => _notifier.NotifyMentionedUsersAsync(
            entry, mentionedUserIds, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task NotifyMentionedUsersAsync_ReturnsCompletedTask()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), new EntityReference("Patient", "1"), TimelineEntryType.Comment,
            "text", new AuthorInfo("user-1", "Bob"), DateTimeOffset.UtcNow, "user-1");

        Task result = _notifier.NotifyMentionedUsersAsync(entry, [], TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        await result;
    }

    [Fact]
    public async Task NotifyReactionToggledAsync_CompletesWithoutThrowing()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), new EntityReference("Patient", "123"), TimelineEntryType.Comment,
            "Test comment", new AuthorInfo("user-1", "Alice"), DateTimeOffset.UtcNow, "user-1");

        Task act() => _notifier.NotifyReactionToggledAsync(
            entry, "user-2", "👍", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task NotifyReactionToggledAsync_ReturnsCompletedTask()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), new EntityReference("Patient", "1"), TimelineEntryType.Comment,
            "text", new AuthorInfo("user-1", "Bob"), DateTimeOffset.UtcNow, "user-1");

        Task result = _notifier.NotifyReactionToggledAsync(entry, "user-2", "👍", TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        await result;
    }
}
