using Granit.Events;
using Granit.Timeline.Events;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineEntrySoftDeletedEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var entryId = Guid.NewGuid();

        TimelineEntrySoftDeletedEvent evt = new(entryId, "Invoice", "inv-42");

        evt.EntryId.ShouldBe(entryId);
        evt.EntityType.ShouldBe("Invoice");
        evt.EntityId.ShouldBe("inv-42");
    }

    [Fact]
    public void ImplementsIDomainEvent() => typeof(TimelineEntrySoftDeletedEvent).GetInterfaces().ShouldContain(typeof(IDomainEvent));
}
