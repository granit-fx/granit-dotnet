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
/// SQLite-backed importer tests — verifies the deep-copy from a registered
/// <see cref="DashboardDefinition"/> to a persisted <see cref="Dashboard"/>
/// aggregate works end-to-end through the real <see cref="DashboardsDbContext"/>.
/// </summary>
public sealed class DashboardImporterTests : IAsyncLifetime
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
    public async Task ImportAsync_UnknownDefinition_ReturnsNull()
    {
        DashboardImporter importer = NewImporter(new InMemoryRegistry());

        Dashboard? result = await importer.ImportAsync("Sample.Missing", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ImportAsync_SingleViewDashboard_CopiesAllWidgets()
    {
        InMemoryRegistry registry = new();
        registry.Register(new SingleViewFinanceDashboard());
        DashboardImporter importer = NewImporter(registry);

        Dashboard? imported = await importer.ImportAsync("Sample.Finance", TestContext.Current.CancellationToken);

        imported.ShouldNotBeNull();
        imported.Status.ShouldBe(DashboardStatus.Draft);
        imported.SourceDefinitionName.ShouldBe("Sample.Finance");
        imported.SourceDefinitionVersion.ShouldBe("1.0.0");
        imported.Widgets.Count.ShouldBe(2);
        imported.Widgets.ShouldContain(w => w.WidgetType == "Markdown");
        imported.Widgets.ShouldContain(w => w.WidgetType == "Text" && w.Position == 1);

        // Round-trip through DbContext to confirm the persistence layer accepted everything.
        await using DashboardsDbContext readBack = NewContext();
        Dashboard fromDb = await readBack.Dashboards
            .Include(d => d.Widgets)
            .SingleAsync(d => d.Id == imported.Id, TestContext.Current.CancellationToken);
        fromDb.Widgets.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ImportAsync_MultiViewDashboard_PicksDefaultViewWidgets()
    {
        InMemoryRegistry registry = new();
        registry.Register(new MultiViewDashboard());
        DashboardImporter importer = NewImporter(registry);

        Dashboard? imported = await importer.ImportAsync("Sample.MultiView", TestContext.Current.CancellationToken);

        imported.ShouldNotBeNull();
        // Default view "list" carries 2 widgets; "detail" carries 1. The import must
        // pick the default view's pool, not the empty top-level Widgets list.
        imported.Widgets.Count.ShouldBe(2);
        imported.Widgets.ShouldContain(w => w.TitleLocalizationKey == "Widget:Sample.MultiView.Title");
    }

    [Fact]
    public async Task ImportAsync_PersistsSourceDefinitionVersion()
    {
        InMemoryRegistry registry = new();
        registry.Register(new VersionedDashboard());
        DashboardImporter importer = NewImporter(registry);

        Dashboard? imported = await importer.ImportAsync("Sample.Versioned", TestContext.Current.CancellationToken);

        imported.ShouldNotBeNull();
        imported.SourceDefinitionVersion.ShouldBe("3.2.1");
    }

    [Fact]
    public async Task ImportAsync_RejectsNullOrWhitespaceName()
    {
        DashboardImporter importer = NewImporter(new InMemoryRegistry());

        await Should.ThrowAsync<ArgumentException>(() =>
            importer.ImportAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    private DashboardsDbContext NewContext()
    {
        DbContextOptions<DashboardsDbContext> options = new DbContextOptionsBuilder<DashboardsDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new DashboardsDbContext(options);
    }

    private DashboardImporter NewImporter(IDashboardDefinitionRegistry registry)
    {
        DashboardsDbContext db = NewContext();
        IGuidGenerator guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        return new DashboardImporter(registry, db, guidGen);
    }

    private sealed class InMemoryRegistry : IDashboardDefinitionRegistry
    {
        private readonly Dictionary<string, IDashboardDefinitionDescriptor> _byName = new(StringComparer.Ordinal);
        public void Register(IDashboardDefinitionDescriptor d) => _byName[d.Name] = d;
        public IReadOnlyList<IDashboardDefinitionDescriptor> GetAll() => [.. _byName.Values];
        public IDashboardDefinitionDescriptor? Find(string name) => _byName.GetValueOrDefault(name);
    }

    private sealed class SingleViewFinanceDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Finance";
        public override DashboardCategory Category => DashboardCategory.Finance;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } =
        [
            new MarkdownWidgetDefinition("Banner", "Widget:Sample.Finance.Banner", Position: 0),
            new TextWidgetDefinition("Heading", "Widget:Sample.Finance.Heading", TextStyle.Heading, Position: 1),
        ];
    }

    private sealed class MultiViewDashboard : DashboardDefinition
    {
        public override string Name => "Sample.MultiView";
        public override DashboardCategory Category => DashboardCategory.Iot;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [];
        public override string? DefaultView => "list";
        public override IReadOnlyList<DashboardView>? Views { get; } =
        [
            new DashboardView("list",
            [
                new TextWidgetDefinition("Title", "Widget:Sample.MultiView.Title", TextStyle.Heading, Position: 0),
                new MarkdownWidgetDefinition("Body", "Widget:Sample.MultiView.Body", Position: 1),
            ]),
            new DashboardView("detail",
            [
                new MarkdownWidgetDefinition("Detail", "Widget:Sample.MultiView.Detail", Position: 0),
            ]),
        ];
    }

    private sealed class VersionedDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Versioned";
        public override DashboardCategory Category => DashboardCategory.General;
        public override string Version => "3.2.1";
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } =
        [
            new MarkdownWidgetDefinition("X", "Widget:X", Position: 0),
        ];
    }
}
