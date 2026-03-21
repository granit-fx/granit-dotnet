using Granit.Core.Events;
using Granit.Timeline.Domain;
using Granit.Timeline.Events;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineEntryPostedEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var entryId = Guid.NewGuid();

        TimelineEntryPostedEvent evt = new(entryId, "Patient", "p-1", TimelineEntryType.Comment, "user-1");

        evt.EntryId.ShouldBe(entryId);
        evt.EntityType.ShouldBe("Patient");
        evt.EntityId.ShouldBe("p-1");
        evt.EntryType.ShouldBe(TimelineEntryType.Comment);
        evt.AuthorId.ShouldBe("user-1");
    }

    [Fact]
    public void ImplementsIDomainEvent() => typeof(TimelineEntryPostedEvent).GetInterfaces().ShouldContain(typeof(IDomainEvent));
}
