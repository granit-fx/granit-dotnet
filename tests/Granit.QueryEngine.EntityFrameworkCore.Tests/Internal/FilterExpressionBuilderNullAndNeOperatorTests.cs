using System.Linq.Expressions;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class FilterExpressionBuilderNullAndNeOperatorTests
{
    [Fact]
    public void Ne_on_string_builds_not_equals_expression()
    {
        FilterCriteria criteria = new("Name", FilterOperator.Ne, "Alice");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldNotBeNull();
        Func<TestProduct, bool> compiled = expr.Compile();
        compiled(new TestProduct { Name = "Bob" }).ShouldBeTrue();
        compiled(new TestProduct { Name = "Alice" }).ShouldBeFalse();
    }

    [Fact]
    public void Ne_on_int()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Ne, "100");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", Price = 150 }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", Price = 100 }).ShouldBeFalse();
    }

    [Fact]
    public void Ne_with_unconvertible_value_returns_null()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Ne, "not-a-number");

        FilterExpressionBuilder.Build<TestProduct>(criteria).ShouldBeNull();
    }

    [Fact]
    public void Ne_follows_two_valued_semantics_null_column_matches()
    {
        // C# lifted != : NULL != <constant> is true, so NULL rows MATCH the Ne criterion.
        FilterCriteria criteria = new("CreatedAt", FilterOperator.Ne, "2024-01-01T00:00:00Z");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", CreatedAt = null }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", CreatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture) }).ShouldBeFalse();
        compiled(new TestProduct { Name = "C", CreatedAt = DateTimeOffset.Parse("2025-06-15T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture) }).ShouldBeTrue();
    }

    [Fact]
    public void IsNull_on_nullable_value_type()
    {
        FilterCriteria criteria = new("CreatedAt", FilterOperator.IsNull, "");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldNotBeNull();
        Func<TestProduct, bool> compiled = expr.Compile();
        compiled(new TestProduct { Name = "A", CreatedAt = null }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", CreatedAt = DateTimeOffset.UtcNow }).ShouldBeFalse();
    }

    [Fact]
    public void IsNotNull_on_nullable_value_type()
    {
        FilterCriteria criteria = new("CreatedAt", FilterOperator.IsNotNull, "");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", CreatedAt = DateTimeOffset.UtcNow }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", CreatedAt = null }).ShouldBeFalse();
    }

    [Fact]
    public void IsNull_on_string_reference_column()
    {
        FilterCriteria criteria = new("Name", FilterOperator.IsNull, "");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = null! }).ShouldBeTrue();
        compiled(new TestProduct { Name = "Alice" }).ShouldBeFalse();
    }

    [Fact]
    public void IsNull_on_non_nullable_value_type_returns_null_in_lenient_mode()
    {
        FilterCriteria criteria = new("Price", FilterOperator.IsNull, "");

        FilterExpressionBuilder.Build<TestProduct>(criteria).ShouldBeNull();
    }

    [Fact]
    public void IsNotNull_on_non_nullable_value_type_returns_null_in_lenient_mode()
    {
        FilterCriteria criteria = new("Activated", FilterOperator.IsNotNull, "");

        FilterExpressionBuilder.Build<TestProduct>(criteria).ShouldBeNull();
    }
}
