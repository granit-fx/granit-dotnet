using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class QueryPredicateTests
{
    [Fact]
    public void Criterion_Builds_FilterPredicate_Leaf()
    {
        var predicate = QueryPredicate.Criterion("Name", FilterOperator.Eq, "Alice");

        FilterPredicate leaf = predicate.ShouldBeOfType<FilterPredicate>();
        leaf.Criteria.ShouldBe(new FilterCriteria("Name", FilterOperator.Eq, "Alice"));
    }

    [Fact]
    public void Criterion_Without_Value_Carries_Empty_String()
    {
        var predicate = QueryPredicate.Criterion("DeletedAt", FilterOperator.IsNull);

        FilterPredicate leaf = predicate.ShouldBeOfType<FilterPredicate>();
        leaf.Criteria.Value.ShouldBe(string.Empty);
        leaf.Criteria.Operator.ShouldBe(FilterOperator.IsNull);
    }

    [Fact]
    public void Criterion_Rejects_Empty_Field()
    {
        Should.Throw<ArgumentException>(() =>
            QueryPredicate.Criterion(" ", FilterOperator.Eq, "x"));
    }

    [Fact]
    public void Criterion_Rejects_Null_Value()
    {
        Should.Throw<ArgumentNullException>(() =>
            QueryPredicate.Criterion("Name", FilterOperator.Eq, null!));
    }

    [Fact]
    public void And_Builds_AndPredicate_With_All_Operands()
    {
        var a = QueryPredicate.Criterion("Name", FilterOperator.Eq, "Alice");
        var b = QueryPredicate.Criterion("Price", FilterOperator.Gt, "10");

        var predicate = QueryPredicate.And(a, b);

        AndPredicate and = predicate.ShouldBeOfType<AndPredicate>();
        and.Operands.Count.ShouldBe(2);
        and.Operands[0].ShouldBe(a);
        and.Operands[1].ShouldBe(b);
    }

    [Fact]
    public void Or_Builds_OrPredicate_With_All_Operands()
    {
        var a = QueryPredicate.Criterion("Name", FilterOperator.Eq, "Alice");
        var b = QueryPredicate.Criterion("Name", FilterOperator.Eq, "Bob");

        var predicate = QueryPredicate.Or(a, b);

        OrPredicate or = predicate.ShouldBeOfType<OrPredicate>();
        or.Operands.Count.ShouldBe(2);
    }

    [Fact]
    public void Not_Wraps_Operand()
    {
        var inner = QueryPredicate.Criterion("Name", FilterOperator.Contains, "li");

        var predicate = QueryPredicate.Not(inner);

        NotPredicate not = predicate.ShouldBeOfType<NotPredicate>();
        not.Operand.ShouldBe(inner);
    }

    [Fact]
    public void And_Rejects_Empty_Operands() => Should.Throw<ArgumentException>(() => QueryPredicate.And());

    [Fact]
    public void Or_Rejects_Empty_Operands() => Should.Throw<ArgumentException>(() => QueryPredicate.Or());

    [Fact]
    public void And_Rejects_Null_Operand()
    {
        Should.Throw<ArgumentNullException>(() =>
            QueryPredicate.And(QueryPredicate.Criterion("Name", FilterOperator.Eq, "x"), null!));
    }

    [Fact]
    public void Not_Rejects_Null_Operand() => Should.Throw<ArgumentNullException>(() => QueryPredicate.Not(null!));

    [Fact]
    public void Factories_Compose_Nested_Trees()
    {
        var predicate = QueryPredicate.Or(
            QueryPredicate.And(
                QueryPredicate.Criterion("Name", FilterOperator.Eq, "Alice"),
                QueryPredicate.Not(QueryPredicate.Criterion("Price", FilterOperator.Lt, "10"))),
            QueryPredicate.Criterion("DeletedAt", FilterOperator.IsNotNull));

        OrPredicate or = predicate.ShouldBeOfType<OrPredicate>();
        or.Operands.Count.ShouldBe(2);
        or.Operands[0].ShouldBeOfType<AndPredicate>();
        or.Operands[1].ShouldBeOfType<FilterPredicate>();
    }

    [Fact]
    public void Structural_Guard_Constants_Are_Pinned()
    {
        // These constants are a wire-adjacent contract for protocol adapters: changing them
        // changes which predicate trees the engine accepts.
        QueryPredicate.MaxDepth.ShouldBe(32);
        QueryPredicate.MaxLeafCount.ShouldBe(128);
    }
}
