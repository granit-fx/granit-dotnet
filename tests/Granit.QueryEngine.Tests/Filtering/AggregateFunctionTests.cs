using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.Filtering;

public sealed class AggregateFunctionTests
{
    [Fact]
    public void All_values_are_defined()
    {
        AggregateFunction[] values = Enum.GetValues<AggregateFunction>();

        values.Length.ShouldBe(5);
    }

    [Theory]
    [InlineData(AggregateFunction.Count, 0)]
    [InlineData(AggregateFunction.Sum, 1)]
    [InlineData(AggregateFunction.Avg, 2)]
    [InlineData(AggregateFunction.Min, 3)]
    [InlineData(AggregateFunction.Max, 4)]
    public void Values_have_expected_ordinals(AggregateFunction function, int expected) => ((int)function).ShouldBe(expected);
}
