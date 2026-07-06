using Granit.Events;
using Granit.Timeline.Events;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineEntrySoftDeletedEventTests
{

    [Fact]
    public void ImplementsIDomainEvent() => typeof(TimelineEntrySoftDeletedEvent).GetInterfaces().ShouldContain(typeof(IDomainEvent));
}
