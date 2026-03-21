using Granit.Querying.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Querying.Tests.Filtering;

public sealed class FilterOperatorTests
{
    [Fact]
    public void All_values_are_defined()
    {
        FilterOperator[] values = Enum.GetValues<FilterOperator>();

        values.Length.ShouldBe(10);
    }

    [Theory]
    [InlineData(FilterOperator.Eq, 0)]
    [InlineData(FilterOperator.Contains, 1)]
    [InlineData(FilterOperator.StartsWith, 2)]
    [InlineData(FilterOperator.EndsWith, 3)]
    [InlineData(FilterOperator.Gt, 4)]
    [InlineData(FilterOperator.Gte, 5)]
    [InlineData(FilterOperator.Lt, 6)]
    [InlineData(FilterOperator.Lte, 7)]
    [InlineData(FilterOperator.In, 8)]
    [InlineData(FilterOperator.Between, 9)]
    public void Values_have_expected_ordinals(FilterOperator op, int expected) => ((int)op).ShouldBe(expected);
}
