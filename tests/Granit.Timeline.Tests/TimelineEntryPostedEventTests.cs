using Granit.Events;
using Granit.Timeline.Events;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineEntryPostedEventTests
{

    [Fact]
    public void ImplementsIDomainEvent() => typeof(TimelineEntryPostedEvent).GetInterfaces().ShouldContain(typeof(IDomainEvent));
}
