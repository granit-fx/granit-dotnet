using System.Linq.Expressions;
using Granit.Domain;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

// Issue #2767 follow-up: equality/IN filters on a SingleValueObject<string> column must build a
// real value-object comparison (EF Core translates whole-value equality through the converter),
// not the previous wrong `column == null` predicate. Substring/range operators stay unsupported.
public sealed class FilterExpressionBuilderValueObjectTests
{
    private static readonly VoProbe[] Rows =
    [
        new() { Slug = ProbeSlug.Create("alpha") },
        new() { Slug = ProbeSlug.Create("beta") },
        new() { Slug = ProbeSlug.Create("gamma") },
    ];

    [Fact]
    public void Eq_on_value_object_builds_a_value_comparison_not_a_null_comparison()
    {
        FilterCriteria criteria = new("Slug", FilterOperator.Eq, "beta");

        Expression<Func<VoProbe, bool>>? expr = FilterExpressionBuilder.Build<VoProbe>(criteria);

        // EF Core translates whole-value equality through the converter (proven against SQLite in
        // the #2767 spike); here we assert the builder produced a real value-object constant — the
        // regression being the previous `column == null` predicate. (Expression.Equal compiled
        // in-memory would use reference equality, so we inspect the tree rather than run it.)
        expr.ShouldNotBeNull();

        ConstantCollector collector = new();
        collector.Visit(expr);
        ConstantExpression constant = collector.Constants.Single(c => c.Type == typeof(ProbeSlug));
        constant.Value.ShouldNotBeNull();
        ((ProbeSlug)constant.Value!).Value.ShouldBe("beta");
    }

    [Fact]
    public void In_on_value_object_matches_the_listed_values()
    {
        FilterCriteria criteria = new("Slug", FilterOperator.In, "alpha,gamma");

        Expression<Func<VoProbe, bool>>? expr = FilterExpressionBuilder.Build<VoProbe>(criteria);

        expr.ShouldNotBeNull();
        Func<VoProbe, bool> match = expr.Compile();
        Rows.Where(match).Select(r => r.Slug.Value).ShouldBe(["alpha", "gamma"], ignoreOrder: true);
    }

    [Theory]
    [InlineData(FilterOperator.Gt)]
    [InlineData(FilterOperator.Lt)]
    [InlineData(FilterOperator.Between)]
    public void Range_operators_on_value_object_are_dropped(FilterOperator op)
    {
        FilterCriteria criteria = new("Slug", op, "alpha,gamma");

        FilterExpressionBuilder.Build<VoProbe>(criteria).ShouldBeNull();
    }

    private sealed class ConstantCollector : ExpressionVisitor
    {
        public List<ConstantExpression> Constants { get; } = [];

        protected override Expression VisitConstant(ConstantExpression node)
        {
            Constants.Add(node);
            return base.VisitConstant(node);
        }
    }

    private sealed class VoProbe
    {
        public ProbeSlug Slug { get; set; } = null!;
    }

    private sealed class ProbeSlug : SingleValueObject<string>
    {
        public override required string Value { get; init; }
        public static ProbeSlug Create(string value) => new() { Value = value };
        public static implicit operator string(ProbeSlug slug) => slug.Value;
    }
}
