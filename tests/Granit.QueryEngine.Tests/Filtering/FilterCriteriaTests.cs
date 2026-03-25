using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.Filtering;

public sealed class FilterCriteriaTests
{
    [Fact]
    public void Properties_are_preserved()
    {
        FilterCriteria criteria = new("name", FilterOperator.Contains, "Alice");

        criteria.Field.ShouldBe("name");
        criteria.Operator.ShouldBe(FilterOperator.Contains);
        criteria.Value.ShouldBe("Alice");
    }

    [Fact]
    public void Record_equality_works()
    {
        FilterCriteria a = new("age", FilterOperator.Gte, "18");
        FilterCriteria b = new("age", FilterOperator.Gte, "18");

        a.ShouldBe(b);
    }

    [Fact]
    public void Different_operator_is_not_equal()
    {
        FilterCriteria a = new("age", FilterOperator.Gt, "18");
        FilterCriteria b = new("age", FilterOperator.Gte, "18");

        a.ShouldNotBe(b);
    }
}
