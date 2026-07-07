using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class DatePeriodTests
{
    [Fact]
    public void All_enum_values_are_defined()
    {
        DatePeriod[] values = Enum.GetValues<DatePeriod>();

        values.Length.ShouldBe(7);
    }

    [Theory]
    [InlineData(DatePeriod.Today, 0)]
    [InlineData(DatePeriod.ThisWeek, 1)]
    [InlineData(DatePeriod.ThisMonth, 2)]
    [InlineData(DatePeriod.LastMonth, 3)]
    [InlineData(DatePeriod.ThisQuarter, 4)]
    [InlineData(DatePeriod.ThisYear, 5)]
    [InlineData(DatePeriod.Custom, 6)]
    public void Enum_values_have_expected_ordinals(DatePeriod period, int expectedOrdinal) => ((int)period).ShouldBe(expectedOrdinal);
}
