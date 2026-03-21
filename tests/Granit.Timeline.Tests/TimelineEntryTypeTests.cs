using Granit.Timeline.Domain;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineEntryTypeTests
{
    [Fact]
    public void Comment_HasValue_0() => ((int)TimelineEntryType.Comment).ShouldBe(0);

    [Fact]
    public void SystemLog_HasValue_1() => ((int)TimelineEntryType.SystemLog).ShouldBe(1);

    [Fact]
    public void InternalNote_HasValue_2() => ((int)TimelineEntryType.InternalNote).ShouldBe(2);

    [Fact]
    public void Enum_HasExactlyThreeValues()
    {
        string[] names = Enum.GetNames<TimelineEntryType>();
        names.Length.ShouldBe(3);
    }
}
