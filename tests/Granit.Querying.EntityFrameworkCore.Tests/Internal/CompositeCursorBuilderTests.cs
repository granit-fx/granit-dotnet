using System.Linq.Expressions;
using Granit.Querying.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Querying.EntityFrameworkCore.Tests.Internal;

public sealed class CompositeCursorBuilderTests
{
    // -------------------------------------------------------------------------
    // ParseSortFields
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseSortFields_parses_ascending_field()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("Price");

        fields.Count.ShouldBe(1);
        fields[0].Property.Name.ShouldBe("Price");
        fields[0].Descending.ShouldBeFalse();
    }

    [Fact]
    public void ParseSortFields_parses_descending_field()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("-Price");

        fields.Count.ShouldBe(1);
        fields[0].Property.Name.ShouldBe("Price");
        fields[0].Descending.ShouldBeTrue();
    }

    [Fact]
    public void ParseSortFields_parses_multiple_fields()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("-Price,Name");

        fields.Count.ShouldBe(2);
        fields[0].Property.Name.ShouldBe("Price");
        fields[0].Descending.ShouldBeTrue();
        fields[1].Property.Name.ShouldBe("Name");
        fields[1].Descending.ShouldBeFalse();
    }

    [Fact]
    public void ParseSortFields_ignores_unknown_properties()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("NonExistent,Name");

        fields.Count.ShouldBe(1);
        fields[0].Property.Name.ShouldBe("Name");
    }

    [Fact]
    public void ParseSortFields_is_case_insensitive()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("price");

        fields.Count.ShouldBe(1);
        fields[0].Property.Name.ShouldBe("Price");
    }

    [Fact]
    public void ParseSortFields_handles_whitespace_between_fields()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>(" Price , Name ");

        fields.Count.ShouldBe(2);
    }

    [Fact]
    public void ParseSortFields_empty_string_returns_empty_list()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("");

        fields.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // BuildCursorPredicate
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildCursorPredicate_returns_null_when_no_sort_fields()
    {
        Expression<Func<TestProduct, bool>>? result = CompositeCursorBuilder.BuildCursorPredicate<TestProduct>(
            [], []);

        result.ShouldBeNull();
    }

    [Fact]
    public void BuildCursorPredicate_returns_null_when_cursor_value_missing()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("Price");

        Expression<Func<TestProduct, bool>>? result = CompositeCursorBuilder.BuildCursorPredicate<TestProduct>(
            fields, []);

        result.ShouldBeNull();
    }

    [Fact]
    public void BuildCursorPredicate_builds_greater_than_for_ascending()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("Price");

        var cursorValues = new Dictionary<string, string> { ["Price"] = "500" };

        Expression<Func<TestProduct, bool>>? predicate = CompositeCursorBuilder.BuildCursorPredicate<TestProduct>(fields, cursorValues);

        predicate.ShouldNotBeNull();

        // Verify: Price > 500 should pass
        Func<TestProduct, bool> compiled = predicate.Compile();
        compiled(new TestProduct { Price = 600 }).ShouldBeTrue();
        compiled(new TestProduct { Price = 500 }).ShouldBeFalse();
        compiled(new TestProduct { Price = 400 }).ShouldBeFalse();
    }

    [Fact]
    public void BuildCursorPredicate_builds_less_than_for_descending()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("-Price");

        var cursorValues = new Dictionary<string, string> { ["Price"] = "500" };

        Expression<Func<TestProduct, bool>>? predicate = CompositeCursorBuilder.BuildCursorPredicate<TestProduct>(fields, cursorValues);

        predicate.ShouldNotBeNull();

        Func<TestProduct, bool> compiled = predicate.Compile();
        compiled(new TestProduct { Price = 400 }).ShouldBeTrue();
        compiled(new TestProduct { Price = 500 }).ShouldBeFalse();
        compiled(new TestProduct { Price = 600 }).ShouldBeFalse();
    }

    [Fact]
    public void BuildCursorPredicate_builds_compound_expression_for_multiple_fields()
    {
        // Sort: -Price, Amount (descending price, then ascending amount)
        // Uses two numeric fields to avoid GreaterThan not defined for String
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("-Price,Amount");

        var cursorValues = new Dictionary<string, string>
        {
            ["Price"] = "500",
            ["Amount"] = "10.5",
        };

        Expression<Func<TestProduct, bool>>? predicate = CompositeCursorBuilder.BuildCursorPredicate<TestProduct>(fields, cursorValues);

        predicate.ShouldNotBeNull();

        Func<TestProduct, bool> compiled = predicate.Compile();

        // Price < 500 → true (first branch: Price < cursor)
        compiled(new TestProduct { Price = 400, Amount = 0m }).ShouldBeTrue();

        // Price == 500, Amount > 10.5 → true (second branch: Price equal, Amount greater)
        compiled(new TestProduct { Price = 500, Amount = 20m }).ShouldBeTrue();

        // Price == 500, Amount < 10.5 → false
        compiled(new TestProduct { Price = 500, Amount = 5m }).ShouldBeFalse();

        // Price > 500 → false
        compiled(new TestProduct { Price = 600, Amount = 0m }).ShouldBeFalse();
    }

    [Fact]
    public void BuildCursorPredicate_returns_null_for_unconvertible_value()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("Price");

        var cursorValues = new Dictionary<string, string> { ["Price"] = "not-a-number" };

        Expression<Func<TestProduct, bool>>? result = CompositeCursorBuilder.BuildCursorPredicate<TestProduct>(fields, cursorValues);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // EncodeCompositeCursor
    // -------------------------------------------------------------------------

    [Fact]
    public void EncodeCompositeCursor_encodes_single_field()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("Price");

        var product = new TestProduct { Id = Guid.NewGuid(), Price = 500, Name = "Test" };

        string cursor = CompositeCursorBuilder.EncodeCompositeCursor(product, fields);

        cursor.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EncodeCompositeCursor_encodes_multiple_fields()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("Price,Name");

        var product = new TestProduct { Id = Guid.NewGuid(), Price = 500, Name = "Laptop" };

        string cursor = CompositeCursorBuilder.EncodeCompositeCursor(product, fields);

        cursor.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EncodeCompositeCursor_roundtrips_with_BuildCursorPredicate()
    {
        List<CompositeCursorBuilder.SortField> fields =
            CompositeCursorBuilder.ParseSortFields<TestProduct>("Price");

        var product = new TestProduct { Id = Guid.NewGuid(), Price = 500, Name = "Laptop" };

        string cursor = CompositeCursorBuilder.EncodeCompositeCursor(product, fields);
        Dictionary<string, string>? cursorValues = CursorEncoder.DecodeComposite(cursor);

        cursorValues.ShouldNotBeNull();

        Expression<Func<TestProduct, bool>>? predicate = CompositeCursorBuilder.BuildCursorPredicate<TestProduct>(fields, cursorValues);

        predicate.ShouldNotBeNull();

        Func<TestProduct, bool> compiled = predicate.Compile();
        // Items with Price > 500 should pass (ascending sort)
        compiled(new TestProduct { Price = 600 }).ShouldBeTrue();
        compiled(new TestProduct { Price = 500 }).ShouldBeFalse();
    }
}
