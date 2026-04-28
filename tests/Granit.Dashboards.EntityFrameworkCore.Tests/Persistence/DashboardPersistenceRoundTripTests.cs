using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.EntityFrameworkCore.Tests.Persistence;

/// <summary>
/// Round-trips a <see cref="Dashboard"/> aggregate through an isolated SQLite-backed
/// <see cref="DashboardsDbContext"/>. Confirms that the EF configuration shape
/// (string columns, JSON config, owned widget collection cascade, layout integers,
/// status enum-as-int) materialises correctly.
/// </summary>
public sealed class DashboardPersistenceRoundTripTests : IAsyncLifetime
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
    public async Task RoundTrip_PreservesAggregateAndWidgets()
    {
        var id = Guid.NewGuid();

        await using (DashboardsDbContext ctx = NewContext())
        {
            var dashboard = Dashboard.Create(
                id,
                "Finance overview",
                DashboardCategory.Finance,
                sourceDefinitionName: "Granit.Invoicing.FinanceOverview",
                sourceDefinitionVersion: "1.0.0");

            dashboard.AddWidget(
                Guid.NewGuid(), "Kpi", 0, 3, 1,
                "Widget:Granit.Invoicing.FinanceOverview.UnpaidCount",
                "{}",
                metricName: "Granit.Invoicing.UnpaidInvoiceCountMetric");

            dashboard.AddWidget(
                Guid.NewGuid(), "Markdown", 1, 12, 1,
                "Widget:Granit.Invoicing.FinanceOverview.Banner",
                "{\"text\":\"Welcome\"}");

            dashboard.Publish();

            ctx.Dashboards.Add(dashboard);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (DashboardsDbContext ctx = NewContext())
        {
            Dashboard loaded = await ctx.Dashboards
                .Include(d => d.Widgets)
                .SingleAsync(d => d.Id == id, TestContext.Current.CancellationToken)
                ;

            loaded.Status.ShouldBe(DashboardStatus.Published);
            loaded.Category.ShouldBe(DashboardCategory.Finance);
            loaded.SourceDefinitionVersion.ShouldBe("1.0.0");
            loaded.LayoutColumns.ShouldBe(12);
            loaded.LayoutRowHeight.ShouldBe(80);
            loaded.Widgets.Count.ShouldBe(2);
            loaded.Widgets.ShouldContain(w => w.WidgetType == "Kpi" && w.MetricName == "Granit.Invoicing.UnpaidInvoiceCountMetric");
            loaded.Widgets.ShouldContain(w => w.WidgetType == "Markdown" && w.ConfigJson.Contains("Welcome", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task DeleteDashboard_CascadesToWidgets()
    {
        var id = Guid.NewGuid();

        await using (DashboardsDbContext ctx = NewContext())
        {
            var dashboard = Dashboard.Create(id, "Ad hoc", DashboardCategory.General);
            dashboard.AddWidget(Guid.NewGuid(), "Markdown", 0, 12, 1, "Widget:X", "{}");
            ctx.Dashboards.Add(dashboard);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (DashboardsDbContext ctx = NewContext())
        {
            Dashboard dashboard = await ctx.Dashboards
                .Include(d => d.Widgets)
                .SingleAsync(d => d.Id == id, TestContext.Current.CancellationToken)
                ;

            ctx.Dashboards.Remove(dashboard);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (DashboardsDbContext ctx = NewContext())
        {
            (await ctx.Dashboards.AnyAsync(d => d.Id == id, TestContext.Current.CancellationToken))
                .ShouldBeFalse();
            (await ctx.WidgetInstances.AnyAsync(TestContext.Current.CancellationToken))
                .ShouldBeFalse();
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
