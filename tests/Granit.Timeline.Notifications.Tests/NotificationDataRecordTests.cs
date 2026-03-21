using Shouldly;
using Xunit;

namespace Granit.Timeline.Notifications.Tests;

public sealed class NotificationDataRecordTests
{
    [Fact]
    public void TimelineCommentNotificationData_SetsAllProperties()
    {
        var entryId = Guid.NewGuid();

        TimelineCommentNotificationData data = new(
            "Patient", "p-1", entryId, "user-1", "Alice", "Hello world");

        data.EntityType.ShouldBe("Patient");
        data.EntityId.ShouldBe("p-1");
        data.EntryId.ShouldBe(entryId);
        data.AuthorId.ShouldBe("user-1");
        data.AuthorName.ShouldBe("Alice");
        data.Body.ShouldBe("Hello world");
    }

    [Fact]
    public void TimelineMentionNotificationData_SetsAllProperties()
    {
        var entryId = Guid.NewGuid();

        TimelineMentionNotificationData data = new(
            "Invoice", "inv-42", entryId, "user-2", "Bob", "@Alice check this");

        data.EntityType.ShouldBe("Invoice");
        data.EntityId.ShouldBe("inv-42");
        data.EntryId.ShouldBe(entryId);
        data.AuthorId.ShouldBe("user-2");
        data.AuthorName.ShouldBe("Bob");
        data.Body.ShouldBe("@Alice check this");
    }

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
}
