using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineStreamEntryTests
{
    [Fact]
    public void DefaultValues_SetCorrectly()
    {
        TimelineStreamEntry entry = new()
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EntryType = TimelineStreamEntryType.Comment,
        };

        entry.AuthorId.ShouldBeNull();
        entry.AuthorName.ShouldBeNull();
        entry.Body.ShouldBe(string.Empty);
        entry.Attachments.ShouldBeEmpty();
        entry.ParentEntryId.ShouldBeNull();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<TimelineAttachmentInfo> attachments =
        [
            new(Guid.NewGuid(), Guid.NewGuid(), "file.txt", "text/plain", 100),
        ];

        TimelineStreamEntry entry = new()
        {
            Id = id,
            OccurredAt = now,
            EntryType = TimelineStreamEntryType.InternalNote,
            AuthorId = "user-1",
            AuthorName = "Alice",
            Body = "Note content",
            Attachments = attachments,
            ParentEntryId = parentId,
        };

        entry.Id.ShouldBe(id);
        entry.OccurredAt.ShouldBe(now);
        entry.EntryType.ShouldBe(TimelineStreamEntryType.InternalNote);
        entry.AuthorId.ShouldBe("user-1");
        entry.AuthorName.ShouldBe("Alice");
        entry.Body.ShouldBe("Note content");
        entry.Attachments.Count.ShouldBe(1);
        entry.ParentEntryId.ShouldBe(parentId);
    }
}
