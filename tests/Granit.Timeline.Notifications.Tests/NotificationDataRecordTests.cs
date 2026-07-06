using Shouldly;
using Xunit;

namespace Granit.Timeline.Notifications.Tests;

public sealed class NotificationDataRecordTests
{

    [Fact]
    public void TimelineCommentNotificationData_RecordEquality()
    {
        var entryId = Guid.NewGuid();

        TimelineCommentNotificationData a = new("Patient", "p-1", entryId, "user-1", "Alice", "Hello");
        TimelineCommentNotificationData b = new("Patient", "p-1", entryId, "user-1", "Alice", "Hello");

        a.ShouldBe(b);
    }

    [Fact]
    public void TimelineMentionNotificationData_RecordEquality()
    {
        var entryId = Guid.NewGuid();

        TimelineMentionNotificationData a = new("Patient", "p-1", entryId, "user-1", "Alice", "Hello");
        TimelineMentionNotificationData b = new("Patient", "p-1", entryId, "user-1", "Alice", "Hello");

        a.ShouldBe(b);
    }

    [Fact]
    public void TimelineReactionNotificationData_RecordEquality()
    {
        var entryId = Guid.NewGuid();

        TimelineReactionNotificationData a = new("Patient", "p-1", entryId, "user-2", "Bob", "👍");
        TimelineReactionNotificationData b = new("Patient", "p-1", entryId, "user-2", "Bob", "👍");

        a.ShouldBe(b);
    }
}
