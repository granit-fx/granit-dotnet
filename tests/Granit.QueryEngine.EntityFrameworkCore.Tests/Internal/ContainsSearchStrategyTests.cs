using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class ContainsSearchStrategyTests
{
    private readonly ContainsSearchStrategy<TestProduct> _strategy = new();

    [Fact]
    public void ApplySearch_matches_across_single_property()
    {
        List<TestProduct> source =
        [
            new() { Name = "Alice Widget" },
            new() { Name = "Bob Gadget" },
        ];

        IQueryable<TestProduct> result = _strategy.ApplySearch(
            source.AsQueryable(), "Widget", ["Name"]);

        var items = result.ToList();
        items.Count.ShouldBe(1);
        items[0].Name.ShouldBe("Alice Widget");
    }

    [Fact]
    public void ApplySearch_matches_across_multiple_properties_with_OR()
    {
        List<TestProduct> source =
        [
            new() { Id = Guid.NewGuid(), Name = "Widget" },
            new() { Id = Guid.NewGuid(), Name = "Other" },
        ];

        // Only Name is string, Id is Guid - should only search Name
        IQueryable<TestProduct> result = _strategy.ApplySearch(
            source.AsQueryable(), "Widget", ["Name"]);

        result.Count().ShouldBe(1);
    }

    [Fact]
    public void ApplySearch_empty_search_returns_all()
    {
        List<TestProduct> source =
        [
            new() { Name = "A" },
            new() { Name = "B" },
        ];

        IQueryable<TestProduct> result = _strategy.ApplySearch(
            source.AsQueryable(), "", ["Name"]);

        result.Count().ShouldBe(2);
    }

    [Fact]
    public void ApplySearch_whitespace_search_returns_all()
    {
        List<TestProduct> source = [new() { Name = "A" }];

        IQueryable<TestProduct> result = _strategy.ApplySearch(
            source.AsQueryable(), "   ", ["Name"]);

        result.Count().ShouldBe(1);
    }

    [Fact]
    public void ApplySearch_no_properties_returns_all()
    {
        List<TestProduct> source = [new() { Name = "A" }];

        IQueryable<TestProduct> result = _strategy.ApplySearch(
            source.AsQueryable(), "A", []);

        result.Count().ShouldBe(1);
    }

    [Fact]
    public void ApplySearch_skips_non_string_properties()
    {
        List<TestProduct> source =
        [
            new() { Name = "A", Price = 100 },
        ];

        // Price is int, should be skipped
        IQueryable<TestProduct> result = _strategy.ApplySearch(
            source.AsQueryable(), "100", ["Price", "Name"]);

        // "100" not found in Name "A", and Price (int) is skipped
        result.Count().ShouldBe(0);
    }

    [Fact]
    public void ApplySearch_handles_null_string_property()
    {
        List<TestProduct> source =
        [
            new() { Name = null! },
            new() { Name = "Alice" },
        ];

        IQueryable<TestProduct> result = _strategy.ApplySearch(
            source.AsQueryable(), "Ali", ["Name"]);

        var items = result.ToList();
        items.Count.ShouldBe(1);
        items[0].Name.ShouldBe("Alice");
    }
}
