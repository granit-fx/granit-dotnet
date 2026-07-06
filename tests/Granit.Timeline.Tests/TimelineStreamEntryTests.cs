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

}
