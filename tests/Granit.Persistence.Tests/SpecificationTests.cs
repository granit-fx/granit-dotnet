using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests;

public sealed class SpecificationTests
{
    [Fact]
    public void Criteria_is_empty_by_default()
    {
        TestSpecification spec = new();

        spec.Criteria.ShouldBeEmpty();
    }

    [Fact]
    public void OrderExpressions_is_empty_by_default()
    {
        TestSpecification spec = new();

        spec.OrderExpressions.ShouldBeEmpty();
    }

    [Fact]
    public void Skip_and_Take_are_null_by_default()
    {
        TestSpecification spec = new();

        spec.Skip.ShouldBeNull();
        spec.Take.ShouldBeNull();
    }

    [Fact]
    public void IsReadOnly_is_false_by_default()
    {
        TestSpecification spec = new();

        spec.IsReadOnly.ShouldBeFalse();
    }

    private sealed class TestSpecification : Specification<FakeEntity>;

    private sealed class FakeEntity;
}
