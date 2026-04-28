using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// SQLite-backed write-side tests for <see cref="DashboardEditor"/> — round-trips
/// the rename + layout update, exercises idempotency on no-op edits, and
/// asserts the domain-guard fallback path (HTTP layer's FluentValidation runs
/// first; these tests confirm the editor still rejects bad input even when
/// validation is bypassed, e.g. internal callers).
/// </summary>
public sealed class DashboardEditorTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        await using DashboardsDbContext seed = NewContext();
        await seed.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task UpdateMetadataAsync_RenamesAndResizesLayout()
    {
        Guid id = await SeedAsync(name: "Old", layoutColumns: 12, layoutRowHeight: 80);
        DashboardEditor editor = new(NewContext());

        DashboardEditResult result = await editor.UpdateMetadataAsync(
            id, "New", layoutColumns: 16, layoutRowHeight: 64, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardEditOutcome.Updated);
        result.Dashboard.ShouldNotBeNull();
        result.Dashboard.Name.ShouldBe("New");
        result.Dashboard.LayoutColumns.ShouldBe(16);
        result.Dashboard.LayoutRowHeight.ShouldBe(64);

        await using DashboardsDbContext readBack = NewContext();
        Dashboard onDisk = await readBack.Dashboards.AsNoTracking().SingleAsync(d => d.Id == id, TestContext.Current.CancellationToken);
        onDisk.Name.ShouldBe("New");
        onDisk.LayoutColumns.ShouldBe(16);
        onDisk.LayoutRowHeight.ShouldBe(64);
    }

    [Fact]
    public async Task UpdateMetadataAsync_NoOpEdit_LeavesAggregateUnchanged()
    {
        Guid id = await SeedAsync(name: "Same", layoutColumns: 12, layoutRowHeight: 80);
        DashboardEditor editor = new(NewContext());

        DashboardEditResult result = await editor.UpdateMetadataAsync(
            id, "Same", layoutColumns: 12, layoutRowHeight: 80, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardEditOutcome.Updated);
        result.Dashboard!.Name.ShouldBe("Same");
        result.Dashboard.LayoutColumns.ShouldBe(12);
        result.Dashboard.LayoutRowHeight.ShouldBe(80);
    }

    [Fact]
    public async Task UpdateMetadataAsync_PreservesWidgetTree()
    {
        Guid id = await SeedAsync(name: "WithWidgets", layoutColumns: 12, layoutRowHeight: 80, widgetCount: 3);
        DashboardEditor editor = new(NewContext());

        DashboardEditResult result = await editor.UpdateMetadataAsync(
            id, "Renamed", layoutColumns: 16, layoutRowHeight: 64, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardEditOutcome.Updated);
        result.Dashboard!.Widgets.Count.ShouldBe(3);

        await using DashboardsDbContext readBack = NewContext();
        Dashboard onDisk = await readBack.Dashboards.AsNoTracking()
            .Include(d => d.Widgets)
            .SingleAsync(d => d.Id == id, TestContext.Current.CancellationToken);
        onDisk.Widgets.Count.ShouldBe(3);
    }

    [Fact]
    public async Task UpdateMetadataAsync_BlankName_ReturnsInvalid()
    {
        Guid id = await SeedAsync(name: "Original", layoutColumns: 12, layoutRowHeight: 80);
        DashboardEditor editor = new(NewContext());

        DashboardEditResult result = await editor.UpdateMetadataAsync(
            id, "   ", layoutColumns: 16, layoutRowHeight: 64, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardEditOutcome.Invalid);
        result.InvalidReason.ShouldNotBeNullOrWhiteSpace();

        await using DashboardsDbContext readBack = NewContext();
        Dashboard onDisk = await readBack.Dashboards.AsNoTracking().SingleAsync(d => d.Id == id, TestContext.Current.CancellationToken);
        onDisk.Name.ShouldBe("Original");
        onDisk.LayoutColumns.ShouldBe(12);
    }

    [Fact]
    public async Task UpdateMetadataAsync_NonPositiveLayoutColumns_ReturnsInvalid()
    {
        Guid id = await SeedAsync(name: "Original", layoutColumns: 12, layoutRowHeight: 80);
        DashboardEditor editor = new(NewContext());

        DashboardEditResult result = await editor.UpdateMetadataAsync(
            id, "Renamed", layoutColumns: 0, layoutRowHeight: 64, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardEditOutcome.Invalid);
    }

    [Fact]
    public async Task UpdateMetadataAsync_NonPositiveLayoutRowHeight_ReturnsInvalid()
    {
        Guid id = await SeedAsync(name: "Original", layoutColumns: 12, layoutRowHeight: 80);
        DashboardEditor editor = new(NewContext());

        DashboardEditResult result = await editor.UpdateMetadataAsync(
            id, "Renamed", layoutColumns: 16, layoutRowHeight: -1, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardEditOutcome.Invalid);
    }

    [Fact]
    public async Task UpdateMetadataAsync_UnknownId_ReturnsNotFound()
    {
        DashboardEditor editor = new(NewContext());

        DashboardEditResult result = await editor.UpdateMetadataAsync(
            Guid.NewGuid(), "Whatever", layoutColumns: 16, layoutRowHeight: 64, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardEditOutcome.NotFound);
        result.Dashboard.ShouldBeNull();
    }

    private async Task<Guid> SeedAsync(string name, int layoutColumns, int layoutRowHeight, int widgetCount = 0)
    {
        var dashboard = Dashboard.Create(
            Guid.NewGuid(), name, DashboardCategory.Finance,
            layoutColumns: layoutColumns, layoutRowHeight: layoutRowHeight);
        for (int i = 0; i < widgetCount; i++)
        {
            dashboard.AddWidget(
                Guid.NewGuid(), "Markdown",
                position: i, width: 12, height: 1,
                titleLocalizationKey: $"Widget:{name}.{i}",
                configJson: "{}");
        }
        dashboard.ClearDomainEvents();

        await using DashboardsDbContext ctx = NewContext();
        ctx.Dashboards.Add(dashboard);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return dashboard.Id;
    }

    private DashboardsDbContext NewContext()
    {
        DbContextOptions<DashboardsDbContext> options = new DbContextOptionsBuilder<DashboardsDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new DashboardsDbContext(options);
    }
}
