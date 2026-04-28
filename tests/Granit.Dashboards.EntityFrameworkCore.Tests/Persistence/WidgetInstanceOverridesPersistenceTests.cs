using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.EntityFrameworkCore.Tests.Persistence;

/// <summary>
/// SQLite-backed round-trip for the JSON-persisted <see cref="WidgetInstanceConfig"/>
/// overrides on <see cref="WidgetInstance"/> rows. Confirms the value converter
/// serialises and deserialises every field, including the nested
/// <see cref="WidgetThreshold"/> list and the operator enum.
/// </summary>
public sealed class WidgetInstanceOverridesPersistenceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        await using DashboardsDbContext ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
        => await _connection.DisposeAsync();

    [Fact]
    public async Task RoundTrip_PreservesAllOverrideFields()
    {
        var dashboardId = Guid.NewGuid();
        var widgetId = Guid.NewGuid();

        await using (DashboardsDbContext ctx = NewContext())
        {
            var dashboard = Dashboard.Create(dashboardId, "Finance", DashboardCategory.Finance);
            WidgetInstance widget = dashboard.AddWidget(
                widgetId, "Kpi", 0, 3, 1,
                "Widget:Sample.Kpi", "{}", metricName: "Sample.Metric");
            widget.ApplyOverrides(new WidgetInstanceConfig(
                TitleLocalizationKeyOverride: "Widget:Custom.Override.Title",
                ColorOverride: "#ff5500",
                UnitOverride: "€",
                DecimalsOverride: 2,
                Thresholds:
                [
                    new WidgetThreshold(100m, "#cc0000", WidgetThresholdOperator.GreaterThanOrEqual),
                    new WidgetThreshold(50m, "#ffaa00", WidgetThresholdOperator.LessThanOrEqual),
                ]));
            ctx.Dashboards.Add(dashboard);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (DashboardsDbContext ctx = NewContext())
        {
            Dashboard loaded = await ctx.Dashboards
                .Include(d => d.Widgets)
                .SingleAsync(d => d.Id == dashboardId, TestContext.Current.CancellationToken);

            WidgetInstance widget = loaded.Widgets.Single(w => w.Id == widgetId);
            widget.Overrides.ShouldNotBeNull();
            widget.Overrides.TitleLocalizationKeyOverride.ShouldBe("Widget:Custom.Override.Title");
            widget.Overrides.ColorOverride.ShouldBe("#ff5500");
            widget.Overrides.UnitOverride.ShouldBe("€");
            widget.Overrides.DecimalsOverride.ShouldBe(2);
            widget.Overrides.Thresholds.ShouldNotBeNull();
            widget.Overrides.Thresholds.Count.ShouldBe(2);
            widget.Overrides.Thresholds[0].Operator.ShouldBe(WidgetThresholdOperator.GreaterThanOrEqual);
            widget.Overrides.Thresholds[0].Value.ShouldBe(100m);
            widget.Overrides.Thresholds[1].Operator.ShouldBe(WidgetThresholdOperator.LessThanOrEqual);
        }
    }

    [Fact]
    public async Task NoOverrides_PersistsAsNullColumn()
    {
        var dashboardId = Guid.NewGuid();
        var widgetId = Guid.NewGuid();

        await using (DashboardsDbContext ctx = NewContext())
        {
            var dashboard = Dashboard.Create(dashboardId, "Finance", DashboardCategory.Finance);
            dashboard.AddWidget(widgetId, "Markdown", 0, 12, 1, "Widget:S.Banner", "{}");
            ctx.Dashboards.Add(dashboard);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (DashboardsDbContext ctx = NewContext())
        {
            WidgetInstance widget = await ctx.WidgetInstances
                .SingleAsync(w => w.Id == widgetId, TestContext.Current.CancellationToken);
            widget.Overrides.ShouldBeNull();
        }
    }

    [Fact]
    public async Task ApplyOverrides_ThenClear_PersistsAsNull()
    {
        var dashboardId = Guid.NewGuid();
        var widgetId = Guid.NewGuid();

        await using (DashboardsDbContext ctx = NewContext())
        {
            var dashboard = Dashboard.Create(dashboardId, "Finance", DashboardCategory.Finance);
            WidgetInstance widget = dashboard.AddWidget(widgetId, "Kpi", 0, 3, 1, "Widget:K", "{}", metricName: "M");
            widget.ApplyOverrides(new WidgetInstanceConfig(ColorOverride: "#000000"));
            ctx.Dashboards.Add(dashboard);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (DashboardsDbContext ctx = NewContext())
        {
            WidgetInstance widget = await ctx.WidgetInstances
                .SingleAsync(w => w.Id == widgetId, TestContext.Current.CancellationToken);
            widget.ApplyOverrides(null);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (DashboardsDbContext ctx = NewContext())
        {
            WidgetInstance widget = await ctx.WidgetInstances
                .SingleAsync(w => w.Id == widgetId, TestContext.Current.CancellationToken);
            widget.Overrides.ShouldBeNull();
        }
    }

    private DashboardsDbContext NewContext()
    {
        DbContextOptions<DashboardsDbContext> options = new DbContextOptionsBuilder<DashboardsDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new DashboardsDbContext(options);
    }
}
