using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class QueryablePaginationExtensionsTests : IAsyncLifetime
{
    private TestDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new TestDbContext(options);

        _db.Products.AddRange(
            new TestProduct { Id = Guid.NewGuid(), Name = "A", Price = 10, Activated = true, Category = ProductCategory.Electronics },
            new TestProduct { Id = Guid.NewGuid(), Name = "B", Price = 20, Activated = true, Category = ProductCategory.Books },
            new TestProduct { Id = Guid.NewGuid(), Name = "C", Price = 30, Activated = true, Category = ProductCategory.Clothing },
            new TestProduct { Id = Guid.NewGuid(), Name = "D", Price = 40, Activated = true, Category = ProductCategory.Electronics },
            new TestProduct { Id = Guid.NewGuid(), Name = "E", Price = 50, Activated = true, Category = ProductCategory.Electronics });

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Offset pagination ────────────────────────────────────────────

    [Fact]
    public async Task ApplyOffsetPagination_first_page_returns_correct_items()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        PagedResult<TestProduct> result = await source.ApplyOffsetPaginationAsync(
            1, 2, precomputedCount: 5, TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(5);
        result.Items[0].Name.ShouldBe("A");
        result.Items[1].Name.ShouldBe("B");
    }

    [Fact]
    public async Task ApplyOffsetPagination_second_page_skips_first()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        PagedResult<TestProduct> result = await source.ApplyOffsetPaginationAsync(
            2, 2, precomputedCount: 5, TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(5);
        result.Items[0].Name.ShouldBe("C");
        result.Items[1].Name.ShouldBe("D");
    }

    [Fact]
    public async Task ApplyOffsetPagination_last_page_returns_remaining_items()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        PagedResult<TestProduct> result = await source.ApplyOffsetPaginationAsync(
            3, 2, precomputedCount: 5, TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(5);
        result.Items[0].Name.ShouldBe("E");
    }

    [Fact]
    public async Task ApplyOffsetPagination_out_of_range_returns_empty()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        PagedResult<TestProduct> result = await source.ApplyOffsetPaginationAsync(
            10, 2, precomputedCount: 5, TestContext.Current.CancellationToken);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(5);
    }

    [Fact]
    public async Task ApplyOffsetPagination_hasMore_true_when_more_pages()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        PagedResult<TestProduct> result = await source.ApplyOffsetPaginationAsync(
            1, 2, precomputedCount: 5, TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyOffsetPagination_hasMore_false_on_last_page()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        PagedResult<TestProduct> result = await source.ApplyOffsetPaginationAsync(
            3, 2, precomputedCount: 5, TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeFalse();
    }

    // ── Offset pagination — null precomputedCount (skip total count) ──

    [Fact]
    public async Task ApplyOffsetPagination_skipTotalCount_returns_null_totalCount()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        PagedResult<TestProduct> result = await source.ApplyOffsetPaginationAsync(
            1, 2, precomputedCount: null, TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBeNull();
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ApplyOffsetPagination_skipTotalCount_hasMore_true_when_more_items()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        PagedResult<TestProduct> result = await source.ApplyOffsetPaginationAsync(
            1, 2, precomputedCount: null, TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeTrue();
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ApplyOffsetPagination_skipTotalCount_hasMore_false_on_last_page()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        PagedResult<TestProduct> result = await source.ApplyOffsetPaginationAsync(
            3, 2, precomputedCount: null, TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeFalse();
        result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ApplyOffsetPagination_skipTotalCount_exact_page_has_no_more()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        // 5 items, pageSize=5 => no extra item fetched
        PagedResult<TestProduct> result = await source.ApplyOffsetPaginationAsync(
            1, 5, precomputedCount: null, TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeFalse();
        result.Items.Count.ShouldBe(5);
        result.TotalCount.ShouldBeNull();
    }

    // ── Cursor pagination ────────────────────────────────────────────

    [Fact]
    public async Task ApplyCursorPagination_null_cursor_returns_first_page()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Price);

        PagedResult<TestProduct> result = await source.ApplyCursorPaginationAsync(
            null, 2, "Price", TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBeNull();
        result.HasMore.ShouldBeTrue();
        result.NextCursor.ShouldNotBeNull();
    }

    [Fact]
    public async Task ApplyCursorPagination_unknown_property_falls_back()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Name);

        PagedResult<TestProduct> result = await source.ApplyCursorPaginationAsync(
            null, 3, "NonExistentProperty", TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ApplyCursorPagination_last_page_has_no_next_cursor()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Price);

        PagedResult<TestProduct> result = await source.ApplyCursorPaginationAsync(
            null, 10, "Price", TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(5);
        result.HasMore.ShouldBeFalse();
        result.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyCursorPagination_with_valid_cursor_filters_items()
    {
        IQueryable<TestProduct> source = _db.Products.OrderBy(p => p.Price);

        // First get a page to obtain a cursor
        PagedResult<TestProduct> firstPage = await source.ApplyCursorPaginationAsync(
            null, 2, "Price", TestContext.Current.CancellationToken);

        firstPage.NextCursor.ShouldNotBeNull();

        // Use the cursor for next page
        PagedResult<TestProduct> secondPage = await source.ApplyCursorPaginationAsync(
            firstPage.NextCursor, 2, "Price", TestContext.Current.CancellationToken);

        // All items should have Price > last item of first page
        int lastPriceFirstPage = firstPage.Items[^1].Price;
        secondPage.Items.ShouldAllBe(p => p.Price > lastPriceFirstPage);
    }
}
