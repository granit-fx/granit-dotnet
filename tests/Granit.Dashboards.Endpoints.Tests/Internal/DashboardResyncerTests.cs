using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.Dashboards.Widgets;
using Granit.Guids;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// SQLite-backed orchestrator tests covering the four
/// <see cref="DashboardResyncOutcome"/> branches end-to-end through the real
/// <see cref="DashboardsDbContext"/>: missing dashboard, ad-hoc dashboard,
/// source-unregistered, and the happy path with override carry-over.
/// </summary>
public sealed class DashboardResyncerTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        await using DashboardsDbContext ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task ResyncAsync_MissingDashboard_ReturnsNotFound()
    {
        DashboardResyncer resyncer = NewResyncer(new InMemoryRegistry());

        DashboardResyncResult result = await resyncer.ResyncAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardResyncOutcome.NotFound);
    }

    [Fact]
    public async Task ResyncAsync_AdHocDashboard_ReturnsNotApplicable()
    {
        var adhoc = Dashboard.Create(
            id: Guid.NewGuid(), name: "Adhoc", category: DashboardCategory.General);
        await using (DashboardsDbContext seed = NewContext())
        {
            seed.Dashboards.Add(adhoc);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        DashboardResyncer resyncer = NewResyncer(new InMemoryRegistry());

        DashboardResyncResult result = await resyncer.ResyncAsync(adhoc.Id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardResyncOutcome.NotApplicable);
        result.ConflictReason.ShouldNotBeNull();
        result.ConflictReason.ShouldContain("ad-hoc");
    }

    [Fact]
    public async Task ResyncAsync_DescriptorNoLongerRegistered_ReturnsSourceUnregistered()
    {
        var imported = Dashboard.Create(
            id: Guid.NewGuid(),
            name: "Sample",
            category: DashboardCategory.Finance,
            sourceDefinitionName: "Sample.Gone",
            sourceDefinitionVersion: "1.0.0");
        await using (DashboardsDbContext seed = NewContext())
        {
            seed.Dashboards.Add(imported);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        DashboardResyncer resyncer = NewResyncer(new InMemoryRegistry()); // empty

        DashboardResyncResult result = await resyncer.ResyncAsync(imported.Id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardResyncOutcome.SourceUnregistered);
        result.ConflictReason.ShouldNotBeNull();
        result.ConflictReason.ShouldContain("Sample.Gone");
    }

    [Fact]
    public async Task ResyncAsync_HappyPath_ReplacesPoolBumpsVersionCarriesOverrides()
    {
        // Seed an imported dashboard at v1.0.0 with one widget that has overrides.
        var imported = Dashboard.Create(
            id: Guid.NewGuid(),
            name: "FinanceLive",
            category: DashboardCategory.Finance,
            sourceDefinitionName: "Sample.Finance",
            sourceDefinitionVersion: "1.0.0");
        WidgetInstance widget = imported.AddWidget(
            widgetId: Guid.NewGuid(),
            widgetType: "Markdown",
            position: 0,
            width: 12,
            height: 1,
            titleLocalizationKey: "Widget:Sample.Finance.Banner",
            configJson: "{\"body\":\"old\"}");
        widget.ApplyOverrides(new WidgetInstanceConfig(ColorOverride: "#cc0000"));
        await using (DashboardsDbContext seed = NewContext())
        {
            seed.Dashboards.Add(imported);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Registry now ships v2.0.0: same Banner slug + a new Heading.
        InMemoryRegistry registry = new();
        registry.Register(new ResyncedFinanceDashboard());
        DashboardResyncer resyncer = NewResyncer(registry);

        DashboardResyncResult result = await resyncer.ResyncAsync(imported.Id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardResyncOutcome.Updated);
        result.Summary.ShouldNotBeNull();
        result.Summary!.PreviousSourceDefinitionVersion.ShouldBe("1.0.0");
        result.Summary.NewSourceDefinitionVersion.ShouldBe("2.0.0");
        result.Summary.WidgetsAdded.ShouldBe(1);
        result.Summary.WidgetsRemoved.ShouldBe(0);
        result.Summary.OverridesCarriedOver.ShouldBe(1);

        // Round-trip through the DbContext to confirm persistence captured everything.
        await using DashboardsDbContext readBack = NewContext();
        Dashboard fromDb = await readBack.Dashboards
            .Include(d => d.Widgets)
            .SingleAsync(d => d.Id == imported.Id, TestContext.Current.CancellationToken);
        fromDb.SourceDefinitionVersion.ShouldBe("2.0.0");
        fromDb.Widgets.Count.ShouldBe(2);
        WidgetInstance carried = fromDb.Widgets.Single(w => w.TitleLocalizationKey == "Widget:Sample.Finance.Banner");
        carried.Overrides.ShouldNotBeNull();
        carried.Overrides!.ColorOverride.ShouldBe("#cc0000");
    }

    private DashboardsDbContext NewContext()
    {
        DbContextOptions<DashboardsDbContext> options = new DbContextOptionsBuilder<DashboardsDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new DashboardsDbContext(options);
    }

    private DashboardResyncer NewResyncer(IDashboardDefinitionRegistry registry)
    {
        DashboardsDbContext db = NewContext();
        IGuidGenerator guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        return new DashboardResyncer(registry, db, guidGen);
    }

    private sealed class InMemoryRegistry : IDashboardDefinitionRegistry
    {
        private readonly Dictionary<string, IDashboardDefinitionDescriptor> _byName = new(StringComparer.Ordinal);
        public void Register(IDashboardDefinitionDescriptor d) => _byName[d.Name] = d;
        public IReadOnlyList<IDashboardDefinitionDescriptor> GetAll() => [.. _byName.Values];
        public IDashboardDefinitionDescriptor? Find(string name) => _byName.GetValueOrDefault(name);
    }

    private sealed class ResyncedFinanceDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Finance";
        public override DashboardCategory Category => DashboardCategory.Finance;
        public override string Version => "2.0.0";
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } =
        [
            new MarkdownWidgetDefinition("Banner", "Widget:Sample.Finance.Banner", Position: 0),
            new TextWidgetDefinition("Heading", "Widget:Sample.Finance.Heading", TextStyle.Heading, Position: 1),
        ];
    }
}
