using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineStreamEntryTypeTests
{
    [Fact]
    public void Comment_HasValue_0() => ((int)TimelineStreamEntryType.Comment).ShouldBe(0);

    [Fact]
    public void InternalNote_HasValue_1() => ((int)TimelineStreamEntryType.InternalNote).ShouldBe(1);

    [Fact]
    public void SystemLog_HasValue_2() => ((int)TimelineStreamEntryType.SystemLog).ShouldBe(2);

    [Fact]
    public void Enum_HasExactlyThreeValues()
    {
        string[] names = Enum.GetNames<TimelineStreamEntryType>();
        names.Length.ShouldBe(3);
    }
}
