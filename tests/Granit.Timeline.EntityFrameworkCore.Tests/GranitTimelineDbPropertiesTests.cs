using Shouldly;
using Xunit;

namespace Granit.Timeline.EntityFrameworkCore.Tests;

public sealed class GranitTimelineDbPropertiesTests
{
    [Fact]
    public void Default_DbTablePrefix_Is_timeline_() => GranitTimelineDbProperties.DbTablePrefix.ShouldBe("timeline_");

    [Fact]
    public void Default_DbSchema_IsNull() => GranitTimelineDbProperties.DbSchema.ShouldBeNull();

}
