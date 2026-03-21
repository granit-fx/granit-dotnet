using System.Linq.Expressions;
using Granit.Querying.EntityFrameworkCore.Internal;
using Granit.Querying.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Querying.EntityFrameworkCore.Tests.Internal;

public sealed class FilterExpressionBuilderAdditionalTests
{
    // ── ConvertValue tests ───────────────────────────────────────────

    [Fact]
    public void ConvertValue_String_returns_same_string()
    {
        object? result = FilterExpressionBuilder.ConvertValue("hello", typeof(string));

        result.ShouldBe("hello");
    }

    [Fact]
    public void ConvertValue_Guid_parses_correctly()
    {
        var expected = Guid.NewGuid();

        object? result = FilterExpressionBuilder.ConvertValue(expected.ToString(), typeof(Guid));

        result.ShouldBe(expected);
    }

    [Fact]
    public void ConvertValue_Bool_parses_true()
    {
        object? result = FilterExpressionBuilder.ConvertValue("true", typeof(bool));

        result.ShouldBe(true);
    }

    [Fact]
    public void ConvertValue_Bool_parses_false()
    {
        object? result = FilterExpressionBuilder.ConvertValue("false", typeof(bool));

        result.ShouldBe(false);
    }

    [Fact]
    public void ConvertValue_DateTimeOffset_parses_iso8601()
    {
        object? result = FilterExpressionBuilder.ConvertValue("2024-06-15T10:30:00Z", typeof(DateTimeOffset));

        result.ShouldBeOfType<DateTimeOffset>();
        var dto = (DateTimeOffset)result!;
        dto.Year.ShouldBe(2024);
        dto.Month.ShouldBe(6);
        dto.Day.ShouldBe(15);
    }

    [Fact]
    public void ConvertValue_DateTime_parses_iso8601()
    {
        object? result = FilterExpressionBuilder.ConvertValue("2024-06-15", typeof(DateTime));

        result.ShouldBeOfType<DateTime>();
        var dt = (DateTime)result!;
        dt.Year.ShouldBe(2024);
        dt.Month.ShouldBe(6);
        dt.Day.ShouldBe(15);
    }

    [Fact]
    public void ConvertValue_DateOnly_parses_correctly()
    {
        object? result = FilterExpressionBuilder.ConvertValue("2024-06-15", typeof(DateOnly));

        result.ShouldBeOfType<DateOnly>();
        var d = (DateOnly)result!;
        d.Year.ShouldBe(2024);
        d.Month.ShouldBe(6);
        d.Day.ShouldBe(15);
    }

    [Fact]
    public void ConvertValue_TimeOnly_parses_correctly()
    {
        object? result = FilterExpressionBuilder.ConvertValue("14:30:00", typeof(TimeOnly));

        result.ShouldBeOfType<TimeOnly>();
        var t = (TimeOnly)result!;
        t.Hour.ShouldBe(14);
        t.Minute.ShouldBe(30);
    }

    [Fact]
    public void ConvertValue_Enum_parses_by_name()
    {
        object? result = FilterExpressionBuilder.ConvertValue("Books", typeof(ProductCategory));

        result.ShouldBe(ProductCategory.Books);
    }

    [Fact]
    public void ConvertValue_Enum_case_insensitive()
    {
        object? result = FilterExpressionBuilder.ConvertValue("electronics", typeof(ProductCategory));

        result.ShouldBe(ProductCategory.Electronics);
    }

    [Fact]
    public void ConvertValue_Int_uses_ChangeType()
    {
        object? result = FilterExpressionBuilder.ConvertValue("42", typeof(int));

        result.ShouldBe(42);
    }

    [Fact]
    public void ConvertValue_Decimal_uses_ChangeType()
    {
        object? result = FilterExpressionBuilder.ConvertValue("99.99", typeof(decimal));

        result.ShouldBe(99.99m);
    }

    [Fact]
    public void ConvertValue_InvalidValue_returns_null()
    {
        object? result = FilterExpressionBuilder.ConvertValue("not-a-guid", typeof(Guid));

        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertValue_InvalidBool_returns_null()
    {
        object? result = FilterExpressionBuilder.ConvertValue("maybe", typeof(bool));

        result.ShouldBeNull();
    }

    // ── Nullable property filter tests ───────────────────────────────

    [Fact]
    public void Gt_on_nullable_date_handles_null_value()
    {
        FilterCriteria criteria = new("CreatedAt", FilterOperator.Gt, "2024-01-01T00:00:00Z");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldNotBeNull();
        Func<TestProduct, bool> compiled = expr.Compile();

        // Null value should return false (HasValue check)
        compiled(new TestProduct { Name = "A", CreatedAt = null }).ShouldBeFalse();

        // Value after cursor should return true
        compiled(new TestProduct { Name = "B", CreatedAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero) }).ShouldBeTrue();

        // Value before cursor should return false
        compiled(new TestProduct { Name = "C", CreatedAt = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero) }).ShouldBeFalse();
    }

    [Fact]
    public void Between_on_nullable_date_handles_null()
    {
        FilterCriteria criteria = new("CreatedAt", FilterOperator.Between, "2024-01-01T00:00:00Z,2024-12-31T23:59:59Z");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldNotBeNull();
        Func<TestProduct, bool> compiled = expr.Compile();

        compiled(new TestProduct { Name = "A", CreatedAt = null }).ShouldBeFalse();
        compiled(new TestProduct { Name = "B", CreatedAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero) }).ShouldBeTrue();
    }

    [Fact]
    public void Contains_on_non_string_returns_null()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Contains, "100");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldBeNull();
    }

    [Fact]
    public void In_with_empty_values_returns_null()
    {
        FilterCriteria criteria = new("Name", FilterOperator.In, "");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldBeNull();
    }

    [Fact]
    public void In_with_unconvertible_values_skips_invalid_items()
    {
        FilterCriteria criteria = new("Price", FilterOperator.In, "10,invalid,30");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldNotBeNull();
        Func<TestProduct, bool> compiled = expr.Compile();
        compiled(new TestProduct { Name = "A", Price = 10 }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", Price = 30 }).ShouldBeTrue();
        compiled(new TestProduct { Name = "C", Price = 20 }).ShouldBeFalse();
    }

    [Fact]
    public void Between_with_three_parts_returns_null()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Between, "10,20,30");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldBeNull();
    }

    [Fact]
    public void Between_with_invalid_lower_returns_null()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Between, "invalid,50");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldBeNull();
    }

    [Fact]
    public void Between_with_invalid_upper_returns_null()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Between, "10,invalid");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldBeNull();
    }

    [Fact]
    public void Eq_on_invalid_value_type_returns_null()
    {
        // Price is int, "abc" can't be converted
        FilterCriteria criteria = new("Price", FilterOperator.Eq, "abc");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldBeNull();
    }

    [Fact]
    public void Gt_on_invalid_value_returns_null()
    {
        FilterCriteria criteria = new("Price", FilterOperator.Gt, "abc");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldBeNull();
    }

    [Fact]
    public void In_on_guid_works()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        FilterCriteria criteria = new("Id", FilterOperator.In, $"{id1},{id2}");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldNotBeNull();
        Func<TestProduct, bool> compiled = expr.Compile();
        compiled(new TestProduct { Id = id1, Name = "A" }).ShouldBeTrue();
        compiled(new TestProduct { Id = id2, Name = "B" }).ShouldBeTrue();
        compiled(new TestProduct { Id = Guid.NewGuid(), Name = "C" }).ShouldBeFalse();
    }

    [Fact]
    public void In_on_enum_works()
    {
        FilterCriteria criteria = new("Category", FilterOperator.In, "Electronics,Books");

        Expression<Func<TestProduct, bool>>? expr = FilterExpressionBuilder.Build<TestProduct>(criteria);

        expr.ShouldNotBeNull();
        Func<TestProduct, bool> compiled = expr.Compile();
        compiled(new TestProduct { Name = "A", Category = ProductCategory.Electronics }).ShouldBeTrue();
        compiled(new TestProduct { Name = "B", Category = ProductCategory.Books }).ShouldBeTrue();
        compiled(new TestProduct { Name = "C", Category = ProductCategory.Clothing }).ShouldBeFalse();
    }
}
