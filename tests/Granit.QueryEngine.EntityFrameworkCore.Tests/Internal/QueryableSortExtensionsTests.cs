using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class QueryableSortExtensionsTests
{
    [Fact]
    public void Sorts_ascending_by_default()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.Column(p => p.Name, c => c.Sortable());

        List<TestProduct> source =
        [
            new() { Name = "Charlie" },
            new() { Name = "Alice" },
            new() { Name = "Bob" },
        ];

        var result = source.AsQueryable()
            .ApplySort("Name", builder)
            .ToList();

        result[0].Name.ShouldBe("Alice");
        result[1].Name.ShouldBe("Bob");
        result[2].Name.ShouldBe("Charlie");
    }

    [Fact]
    public void Sorts_descending_with_prefix()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.Column(p => p.Price, c => c.Sortable());

        List<TestProduct> source =
        [
            new() { Name = "A", Price = 10 },
            new() { Name = "B", Price = 30 },
            new() { Name = "C", Price = 20 },
        ];

        var result = source.AsQueryable()
            .ApplySort("-Price", builder)
            .ToList();

        result[0].Price.ShouldBe(30);
        result[1].Price.ShouldBe(20);
        result[2].Price.ShouldBe(10);
    }

    [Fact]
    public void Multi_column_sort()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder
            .Column(p => p.Category, c => c.Sortable())
            .Column(p => p.Name, c => c.Sortable());

        // Electronics=0, Books=1 — so Electronics sorts first
        List<TestProduct> source =
        [
            new() { Name = "B", Category = ProductCategory.Books },
            new() { Name = "A", Category = ProductCategory.Books },
            new() { Name = "C", Category = ProductCategory.Electronics },
        ];

        var result = source.AsQueryable()
            .ApplySort("Category,Name", builder)
            .ToList();

        result[0].Name.ShouldBe("C"); // Electronics (0)
        result[1].Name.ShouldBe("A"); // Books (1)
        result[2].Name.ShouldBe("B"); // Books (1)
    }

    [Fact]
    public void Ignores_non_sortable_fields()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.Column(p => p.Name, c => c.Sortable());
        // Price is NOT sortable

        List<TestProduct> source =
        [
            new() { Name = "B", Price = 10 },
            new() { Name = "A", Price = 20 },
        ];

        var result = source.AsQueryable()
            .ApplySort("Price,Name", builder)
            .ToList();

        // Only Name sort is applied
        result[0].Name.ShouldBe("A");
        result[1].Name.ShouldBe("B");
    }

    [Fact]
    public void Uses_default_sort_when_null()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder
            .Column(p => p.Name, c => c.Sortable())
            .DefaultSort("-Name");

        List<TestProduct> source =
        [
            new() { Name = "Alice" },
            new() { Name = "Charlie" },
            new() { Name = "Bob" },
        ];

        var result = source.AsQueryable()
            .ApplySort(null, builder)
            .ToList();

        result[0].Name.ShouldBe("Charlie");
        result[1].Name.ShouldBe("Bob");
        result[2].Name.ShouldBe("Alice");
    }

    [Fact]
    public void Empty_sort_and_no_default_falls_back_to_id()
    {
        // No request sort, no DefaultSort → must still order deterministically (by Id)
        // so downstream Skip/Take is stable and EF Core does not warn.
        QueryDefinitionBuilder<TestProduct> builder = new();
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        List<TestProduct> source =
        [
            new() { Id = second, Name = "B" },
            new() { Id = first, Name = "A" },
        ];

        var result = source.AsQueryable()
            .ApplySort("", builder)
            .ToList();

        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe(first);
        result[1].Id.ShouldBe(second);
    }

    [Fact]
    public void Non_whitelisted_only_sort_falls_back_to_cursor_key()
    {
        // Requested field is not sortable → nothing resolves → fall back to the cursor key.
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.SupportsCursorPagination(p => p.Price);

        List<TestProduct> source =
        [
            new() { Name = "B", Price = 30 },
            new() { Name = "A", Price = 10 },
            new() { Name = "C", Price = 20 },
        ];

        var result = source.AsQueryable()
            .ApplySort("Name", builder)
            .ToList();

        result[0].Price.ShouldBe(10);
        result[1].Price.ShouldBe(20);
        result[2].Price.ShouldBe(30);
    }
}
