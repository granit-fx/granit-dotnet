using System.Linq.Expressions;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class FilterExpressionBuilderTests
{
    [Fact]
    public void Eq_on_string_builds_equals_expression()
    {
        FilterCriteria criteria = new("Name", FilterOperator.Eq, "Alice");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldNotBeNull();
        Func<TestProduct, bool> compiled = expr.Compile();
        compiled(new TestProduct { Name = "Alice" }).ShouldBeTrue();
        compiled(new TestProduct { Name = "Bob" }).ShouldBeFalse();
    }

    [Fact]
    public void Contains_on_string_builds_contains_expression()
    {
        FilterCriteria criteria = new("Name", FilterOperator.Contains, "li");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldNotBeNull();
        Func<TestProduct, bool> compiled = expr.Compile();
        compiled(new TestProduct { Name = "Alice" }).ShouldBeTrue();
        compiled(new TestProduct { Name = "Bob" }).ShouldBeFalse();
    }

    [Fact]
    public void Contains_on_null_string_returns_false()
    {
        FilterCriteria criteria = new("Name", FilterOperator.Contains, "test");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = null! }).ShouldBeFalse();
    }

    [Fact]
    public void StartsWith_on_string()
    {
        FilterCriteria criteria = new("Name", FilterOperator.StartsWith, "Al");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "Alice" }).ShouldBeTrue();
        compiled(new TestProduct { Name = "Bob" }).ShouldBeFalse();
    }

    [Fact]
    public void EndsWith_on_string()
    {
        FilterCriteria criteria = new("Name", FilterOperator.EndsWith, "ce");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "Alice" }).ShouldBeTrue();
        compiled(new TestProduct { Name = "Bob" }).ShouldBeFalse();
    }

    [Fact]
    public void Gt_on_int()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Gt, "100");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", Price = 150 }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", Price = 100 }).ShouldBeFalse();
        compiled(new TestProduct { Name = "C", Price = 50 }).ShouldBeFalse();
    }

    [Fact]
    public void Gte_on_decimal()
    {
        FilterCriteria criteria = new("Amount", FilterOperator.Gte, "99.99");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", Amount = 100m }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", Amount = 99.99m }).ShouldBeTrue();
        compiled(new TestProduct { Name = "C", Amount = 50m }).ShouldBeFalse();
    }

    [Fact]
    public void Lt_on_int()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Lt, "100");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", Price = 50 }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", Price = 100 }).ShouldBeFalse();
    }

    [Fact]
    public void Lte_on_int()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Lte, "100");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", Price = 100 }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", Price = 101 }).ShouldBeFalse();
    }

    [Fact]
    public void In_on_string()
    {
        FilterCriteria criteria = new("Name", FilterOperator.In, "Alice,Bob");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "Alice" }).ShouldBeTrue();
        compiled(new TestProduct { Name = "Bob" }).ShouldBeTrue();
        compiled(new TestProduct { Name = "Charlie" }).ShouldBeFalse();
    }

    [Fact]
    public void In_on_int()
    {
        FilterCriteria criteria = new("Price", FilterOperator.In, "10,20,30");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", Price = 20 }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", Price = 25 }).ShouldBeFalse();
    }

    [Fact]
    public void Between_on_int()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Between, "10,50");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", Price = 10 }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", Price = 30 }).ShouldBeTrue();
        compiled(new TestProduct { Name = "C", Price = 50 }).ShouldBeTrue();
        compiled(new TestProduct { Name = "D", Price = 51 }).ShouldBeFalse();
        compiled(new TestProduct { Name = "E", Price = 9 }).ShouldBeFalse();
    }

    [Fact]
    public void Eq_on_bool()
    {
        FilterCriteria criteria = new("IsActive", FilterOperator.Eq, "true");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", IsActive = true }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", IsActive = false }).ShouldBeFalse();
    }

    [Fact]
    public void Eq_on_enum()
    {
        FilterCriteria criteria = new("Category", FilterOperator.Eq, "Electronics");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Name = "A", Category = ProductCategory.Electronics }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", Category = ProductCategory.Books }).ShouldBeFalse();
    }

    [Fact]
    public void Unknown_property_returns_null()
    {
        FilterCriteria criteria = new("NonExistent", FilterOperator.Eq, "test");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldBeNull();
    }

    [Fact]
    public void Invalid_value_returns_null()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Eq, "not-a-number");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldBeNull();
    }

    [Fact]
    public void Between_with_wrong_format_returns_null()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Between, "only-one-value");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldBeNull();
    }

    [Fact]
    public void Case_insensitive_property_name()
    {
        FilterCriteria criteria = new("name", FilterOperator.Eq, "Alice");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldNotBeNull();
        Func<TestProduct, bool> compiled = expr.Compile();
        compiled(new TestProduct { Name = "Alice" }).ShouldBeTrue();
    }

    [Fact]
    public void Eq_on_guid()
    {
        var id = Guid.NewGuid();
        FilterCriteria criteria = new("Id", FilterOperator.Eq, id.ToString());

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        Func<TestProduct, bool> compiled = expr!.Compile();
        compiled(new TestProduct { Id = id, Name = "A" }).ShouldBeTrue();
        compiled(new TestProduct { Id = Guid.NewGuid(), Name = "B" }).ShouldBeFalse();
    }
}

public sealed class TestProduct
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; }
    public decimal Amount { get; set; }
    public bool IsActive { get; set; }
    public ProductCategory Category { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}

public enum ProductCategory
{
    Electronics,
    Books,
    Clothing,
}
