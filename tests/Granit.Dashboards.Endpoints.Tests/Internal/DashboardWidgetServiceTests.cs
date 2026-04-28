using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.Guids;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// SQLite-backed tests for <see cref="DashboardWidgetService"/> — round-trips
/// the add + remove operations through the real <see cref="DashboardsDbContext"/>,
/// asserts the domain-guard fallback path, and locks the dashboard-vs-widget
/// 404 distinction (the 404 reason matters for caller diagnostics).
/// </summary>
public sealed class DashboardWidgetServiceTests : IAsyncLifetime
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
    public async Task AddWidgetAsync_AppendsToWidgetPool_AndReturnsCreated()
    {
        Guid dashboardId = await SeedAsync(widgetCount: 1);
        DashboardWidgetService service = NewService();

        DashboardWidgetAddResult result = await service.AddWidgetAsync(
            dashboardId, "Markdown", position: 1, width: 6, height: 1,
            titleLocalizationKey: "Widget:Sample.Two",
            configJson: "{}",
            metricName: null, queryName: null, requiredPermission: null,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardWidgetAddOutcome.Created);
        result.Widget.ShouldNotBeNull();
        result.Widget.WidgetType.ShouldBe("Markdown");
        result.Widget.Position.ShouldBe(1);

        await using DashboardsDbContext readBack = NewContext();
        Dashboard onDisk = await readBack.Dashboards.AsNoTracking()
            .Include(d => d.Widgets)
            .SingleAsync(d => d.Id == dashboardId, TestContext.Current.CancellationToken);
        onDisk.Widgets.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddWidgetAsync_PreservesAllOptionalFields()
    {
        Guid dashboardId = await SeedAsync();
        DashboardWidgetService service = NewService();

        DashboardWidgetAddResult result = await service.AddWidgetAsync(
            dashboardId, "Kpi", position: 0, width: 3, height: 1,
            titleLocalizationKey: "Widget:Sample.UnpaidCount",
            configJson: "{\"kind\":\"metric\"}",
            metricName: "Sample.Metric",
            queryName: null,
            requiredPermission: "Custom.Composite.Read",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardWidgetAddOutcome.Created);
        result.Widget!.MetricName.ShouldBe("Sample.Metric");
        result.Widget.QueryName.ShouldBeNull();
        result.Widget.RequiredPermission.ShouldBe("Custom.Composite.Read");
    }

    [Fact]
    public async Task AddWidgetAsync_AllocatesWidgetIdServerSide()
    {
        Guid dashboardId = await SeedAsync();
        var expected = Guid.NewGuid();
        IGuidGenerator gen = Substitute.For<IGuidGenerator>();
        gen.Create().Returns(expected);
        DashboardWidgetService service = new(NewContext(), gen);

        DashboardWidgetAddResult result = await service.AddWidgetAsync(
            dashboardId, "Markdown", 0, 6, 1, "Widget:K", "{}", null, null, null,
            TestContext.Current.CancellationToken);

        result.Widget!.Id.ShouldBe(expected);
    }

    [Fact]
    public async Task AddWidgetAsync_BlankWidgetType_ReturnsInvalid()
    {
        Guid dashboardId = await SeedAsync();
        DashboardWidgetService service = NewService();

        DashboardWidgetAddResult result = await service.AddWidgetAsync(
            dashboardId, "  ", 0, 6, 1, "Widget:K", "{}", null, null, null,
            TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardWidgetAddOutcome.Invalid);
        result.InvalidReason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AddWidgetAsync_NegativePosition_ReturnsInvalid()
    {
        Guid dashboardId = await SeedAsync();
        DashboardWidgetService service = NewService();

        DashboardWidgetAddResult result = await service.AddWidgetAsync(
            dashboardId, "Markdown", -1, 6, 1, "Widget:K", "{}", null, null, null,
            TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardWidgetAddOutcome.Invalid);
    }

    [Fact]
    public async Task AddWidgetAsync_NonPositiveWidth_ReturnsInvalid()
    {
        Guid dashboardId = await SeedAsync();
        DashboardWidgetService service = NewService();

        DashboardWidgetAddResult result = await service.AddWidgetAsync(
            dashboardId, "Markdown", 0, 0, 1, "Widget:K", "{}", null, null, null,
            TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardWidgetAddOutcome.Invalid);
    }

    [Fact]
    public async Task AddWidgetAsync_UnknownDashboard_ReturnsNotFound()
    {
        DashboardWidgetService service = NewService();

        DashboardWidgetAddResult result = await service.AddWidgetAsync(
            Guid.NewGuid(), "Markdown", 0, 6, 1, "Widget:K", "{}", null, null, null,
            TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardWidgetAddOutcome.NotFound);
        result.Widget.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveWidgetAsync_RemovesPinnedWidget_AndPersists()
    {
        (Guid dashboardId, Guid widgetId) = await SeedWithKnownWidgetAsync();
        DashboardWidgetService service = NewService();

        DashboardWidgetRemoveResult result = await service.RemoveWidgetAsync(dashboardId, widgetId, TestContext.Current.CancellationToken);

        result.ShouldBe(DashboardWidgetRemoveResult.Removed);

        await using DashboardsDbContext readBack = NewContext();
        Dashboard onDisk = await readBack.Dashboards.AsNoTracking()
            .Include(d => d.Widgets)
            .SingleAsync(d => d.Id == dashboardId, TestContext.Current.CancellationToken);
        onDisk.Widgets.ShouldNotContain(w => w.Id == widgetId);
    }

    [Fact]
    public async Task RemoveWidgetAsync_UnknownDashboard_ReturnsDashboardNotFound()
    {
        DashboardWidgetService service = NewService();

        DashboardWidgetRemoveResult result = await service.RemoveWidgetAsync(
            Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBe(DashboardWidgetRemoveResult.DashboardNotFound);
    }

    [Fact]
    public async Task RemoveWidgetAsync_UnknownWidgetOnExistingDashboard_ReturnsWidgetNotFound()
    {
        Guid dashboardId = await SeedAsync(widgetCount: 1);
        DashboardWidgetService service = NewService();

        DashboardWidgetRemoveResult result = await service.RemoveWidgetAsync(
            dashboardId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBe(DashboardWidgetRemoveResult.WidgetNotFound);
    }

    private async Task<Guid> SeedAsync(int widgetCount = 0)
    {
        var dashboard = Dashboard.Create(Guid.NewGuid(), "Sample", DashboardCategory.Finance);
        for (int i = 0; i < widgetCount; i++)
        {
            dashboard.AddWidget(
                Guid.NewGuid(), "Markdown",
                position: i, width: 12, height: 1,
                titleLocalizationKey: $"Widget:Sample.{i}",
                configJson: "{}");
        }
        dashboard.ClearDomainEvents();

        await using DashboardsDbContext ctx = NewContext();
        ctx.Dashboards.Add(dashboard);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return dashboard.Id;
    }

    private async Task<(Guid dashboardId, Guid widgetId)> SeedWithKnownWidgetAsync()
    {
        var dashboard = Dashboard.Create(Guid.NewGuid(), "Sample", DashboardCategory.Finance);
        var widgetId = Guid.NewGuid();
        dashboard.AddWidget(widgetId, "Markdown",
            position: 0, width: 12, height: 1,
            titleLocalizationKey: "Widget:Sample.0",
            configJson: "{}");
        dashboard.ClearDomainEvents();

        await using DashboardsDbContext ctx = NewContext();
        ctx.Dashboards.Add(dashboard);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (dashboard.Id, widgetId);
    }

    private DashboardWidgetService NewService()
    {
        IGuidGenerator gen = Substitute.For<IGuidGenerator>();
        gen.Create().Returns(_ => Guid.NewGuid());
        return new DashboardWidgetService(NewContext(), gen);
    }

    private DashboardsDbContext NewContext()
    {
        DbContextOptions<DashboardsDbContext> options = new DbContextOptionsBuilder<DashboardsDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new DashboardsDbContext(options);
    }
}
