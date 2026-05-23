using Granit.Timeline.Domain;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineStreamEntryTypeTests
{
    [Fact]
    public void Comment_HasValue_0() => ((int)TimelineStreamEntryType.Comment).ShouldBe(0);

    [Fact]
    public void SystemLog_HasValue_1() => ((int)TimelineStreamEntryType.SystemLog).ShouldBe(1);

    [Fact]
    public void InternalNote_HasValue_2() => ((int)TimelineStreamEntryType.InternalNote).ShouldBe(2);

    [Fact]
    public void Enum_HasExactlyThreeValues()
    {
        string[] names = Enum.GetNames<TimelineStreamEntryType>();
        names.Length.ShouldBe(3);
    }

    [Fact]
    public void StreamEnum_NumericValues_MatchDomainEnum()
    {
        // Guard: the projection from Domain.TimelineEntryType to TimelineStreamEntryType is a
        // direct cast — any divergence silently mislabels entries on the wire.
        ((int)TimelineStreamEntryType.Comment).ShouldBe((int)TimelineEntryType.Comment);
        ((int)TimelineStreamEntryType.SystemLog).ShouldBe((int)TimelineEntryType.SystemLog);
        ((int)TimelineStreamEntryType.InternalNote).ShouldBe((int)TimelineEntryType.InternalNote);
    }
}
