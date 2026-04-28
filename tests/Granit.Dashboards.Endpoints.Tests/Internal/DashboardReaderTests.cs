using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.Dashboards.Widgets;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// SQLite-backed read-side tests — verifies status filtering, paging, ordering,
/// and the eager-load behaviour of <see cref="DashboardReader"/>. Pure
/// SQL-on-aggregate, no HTTP harness.
/// </summary>
public sealed class DashboardReaderTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        await using DashboardsDbContext seed = NewContext();
        await seed.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        seed.Dashboards.AddRange(
            DashboardWith("Alpha", DashboardCategory.Finance, DashboardStatus.Draft, widgetCount: 2),
            DashboardWith("Beta", DashboardCategory.Operations, DashboardStatus.Published, widgetCount: 0),
            DashboardWith("Gamma", DashboardCategory.Finance, DashboardStatus.Published, widgetCount: 1),
            DashboardWith("Delta", DashboardCategory.Iot, DashboardStatus.Archived, widgetCount: 3));
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task ListAsync_NoStatusFilter_ReturnsAllOrderedByName()
    {
        DashboardReader reader = new(NewContext());

        DashboardListPage page = await reader.ListAsync(status: null, page: 0, pageSize: 50, TestContext.Current.CancellationToken);

        page.TotalCount.ShouldBe(4);
        page.Items.Select(d => d.Name).ShouldBe(["Alpha", "Beta", "Delta", "Gamma"]);
    }

    [Fact]
    public async Task ListAsync_StatusFilter_NarrowsToMatchingRows()
    {
        DashboardReader reader = new(NewContext());

        DashboardListPage page = await reader.ListAsync(DashboardStatus.Published, page: 0, pageSize: 50, TestContext.Current.CancellationToken);

        page.TotalCount.ShouldBe(2);
        page.Items.Select(d => d.Name).ShouldBe(["Beta", "Gamma"]);
    }

    [Fact]
    public async Task ListAsync_PagingSlicesByPageSize_AndReportsTotal()
    {
        DashboardReader reader = new(NewContext());

        DashboardListPage page0 = await reader.ListAsync(status: null, page: 0, pageSize: 2, TestContext.Current.CancellationToken);
        DashboardListPage page1 = await reader.ListAsync(status: null, page: 1, pageSize: 2, TestContext.Current.CancellationToken);

        page0.Items.Count.ShouldBe(2);
        page0.Items.Select(d => d.Name).ShouldBe(["Alpha", "Beta"]);
        page0.TotalCount.ShouldBe(4);

        page1.Items.Count.ShouldBe(2);
        page1.Items.Select(d => d.Name).ShouldBe(["Delta", "Gamma"]);
        page1.TotalCount.ShouldBe(4);
    }

    [Fact]
    public async Task ListAsync_NegativePage_Throws()
    {
        DashboardReader reader = new(NewContext());

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            reader.ListAsync(status: null, page: -1, pageSize: 10, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_PageSizeOutOfBounds_Throws()
    {
        DashboardReader reader = new(NewContext());

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            reader.ListAsync(status: null, page: 0, pageSize: 0, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            reader.ListAsync(status: null, page: 0, pageSize: 201, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindByIdAsync_KnownId_EagerLoadsWidgets()
    {
        Guid alphaId;
        await using (DashboardsDbContext ctx = NewContext())
        {
            alphaId = await ctx.Dashboards.Where(d => d.Name == "Alpha").Select(d => d.Id).SingleAsync(TestContext.Current.CancellationToken);
        }

        DashboardReader reader = new(NewContext());
        Dashboard? loaded = await reader.FindByIdAsync(alphaId, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Widgets.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FindByIdAsync_UnknownId_ReturnsNull()
    {
        DashboardReader reader = new(NewContext());

        Dashboard? loaded = await reader.FindByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        loaded.ShouldBeNull();
    }

    private DashboardsDbContext NewContext()
    {
        DbContextOptions<DashboardsDbContext> options = new DbContextOptionsBuilder<DashboardsDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new DashboardsDbContext(options);
    }

    private static Dashboard DashboardWith(string name, DashboardCategory category, DashboardStatus status, int widgetCount)
    {
        var d = Dashboard.Create(Guid.NewGuid(), name, category);
        for (int i = 0; i < widgetCount; i++)
        {
            d.AddWidget(
                Guid.NewGuid(), "Markdown",
                position: i, width: 12, height: 1,
                titleLocalizationKey: $"Widget:{name}.{i}",
                configJson: "{}");
        }

        if (status == DashboardStatus.Published)
        {
            d.Publish();
        }
        else if (status == DashboardStatus.Archived)
        {
            d.Publish();
            d.Archive();
        }

        d.ClearDomainEvents();
        return d;
    }
}
