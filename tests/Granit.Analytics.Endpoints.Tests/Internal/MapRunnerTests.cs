using System.Runtime.CompilerServices;
using System.Text.Json;
using Granit.Analytics.Endpoints.Internal;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

/// <summary>
/// Unit tests for <see cref="MapRunner{TEntity}"/> — substitute the
/// <see cref="IQueryEngine{TEntity}.ExecuteStreamAsync"/> path with NSubstitute
/// so the streaming + reflection-based projection logic is exercised against
/// a known dataset, independent of EF Core translation. Skip-row semantics
/// (null lat/lng), id resolution, popup whitelist, dashboard filter
/// forwarding, and every config-error path are pinned here.
/// </summary>
public sealed class MapRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_PopulatedRows_ProjectsToMapPoints()
    {
        TestItem[] items =
        [
            new() { Id = Guid.NewGuid(), Name = "Paris", Latitude = 48.85, Longitude = 2.35 },
            new() { Id = Guid.NewGuid(), Name = "London", Latitude = 51.50, Longitude = -0.12 },
        ];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        MapRunnerResult result = await runner.ExecuteAsync(
            latitudeColumn: "Latitude",
            longitudeColumn: "Longitude",
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Points.Count.ShouldBe(2);
        result.Points[0].Latitude.ShouldBe(48.85);
        result.Points[0].Longitude.ShouldBe(2.35);
        result.Points[1].Latitude.ShouldBe(51.50);
    }

    [Fact]
    public async Task ExecuteAsync_RowsWithNullCoordinates_AreSkipped()
    {
        // A row missing lat or lng would be pinned at (0, 0) — that's
        // misleading. The runner drops it entirely so the frontend never
        // shows a misplaced marker.
        TestItem[] items =
        [
            new() { Id = Guid.NewGuid(), Name = "Paris", Latitude = 48.85, Longitude = 2.35 },
            new() { Id = Guid.NewGuid(), Name = "Mystery", LatitudeNullable = null, LongitudeNullable = null },
        ];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        MapRunnerResult result = await runner.ExecuteAsync(
            latitudeColumn: "LatitudeNullable",
            longitudeColumn: "LongitudeNullable",
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        // Both rows have null nullable coords on the second item → only
        // the first row had coordinates set on the canonical Latitude /
        // Longitude. So neither row contributes to the nullable path.
        result.Points.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_GuidIdProperty_PropagatesAsMarkerId()
    {
        var id = Guid.NewGuid();
        TestItem[] items = [new() { Id = id, Latitude = 48.85, Longitude = 2.35 }];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        MapRunnerResult result = await runner.ExecuteAsync(
            latitudeColumn: "Latitude",
            longitudeColumn: "Longitude",
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Points[0].Id.ShouldBe(id);
    }

    [Fact]
    public async Task ExecuteAsync_PopupColumns_ProjectWhitelistedFields()
    {
        TestItem[] items = [new()
        {
            Id = Guid.NewGuid(),
            Name = "Paris",
            Country = "FR",
            Latitude = 48.85,
            Longitude = 2.35,
        }];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        MapRunnerResult result = await runner.ExecuteAsync(
            latitudeColumn: "Latitude",
            longitudeColumn: "Longitude",
            popupColumns: ["Name", "Country"],
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Points[0].Popup.ShouldNotBeNull();
        JsonElement popup = result.Points[0].Popup!.Value;
        popup.GetProperty("name").GetString().ShouldBe("Paris");
        popup.GetProperty("country").GetString().ShouldBe("FR");
    }

    [Fact]
    public async Task ExecuteAsync_NoPopupColumns_LeavesPopupNull()
    {
        TestItem[] items = [new() { Id = Guid.NewGuid(), Latitude = 0d, Longitude = 0d }];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        MapRunnerResult result = await runner.ExecuteAsync(
            latitudeColumn: "Latitude",
            longitudeColumn: "Longitude",
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Points[0].Popup.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DecimalCoordinateColumns_AreSupported()
    {
        // Some entities use decimal lat/lng (financial-grade precision).
        // The runner should accept either double or decimal.
        TestItem[] items = [new()
        {
            Id = Guid.NewGuid(),
            LatitudeDecimal = 48.85m,
            LongitudeDecimal = 2.35m,
        }];

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine);

        MapRunnerResult result = await runner.ExecuteAsync(
            latitudeColumn: "LatitudeDecimal",
            longitudeColumn: "LongitudeDecimal",
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Points[0].Latitude.ShouldBe(48.85);
        result.Points[0].Longitude.ShouldBe(2.35);
    }

    [Fact]
    public async Task ExecuteAsync_NonNumericCoordinateColumn_Throws()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                latitudeColumn: "Name", // string column
                longitudeColumn: "Longitude",
                popupColumns: null,
                dashboardFilters: null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownLatitudeColumn_Throws()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        ArgumentException ex = await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                latitudeColumn: "NotAColumn",
                longitudeColumn: "Longitude",
                popupColumns: null,
                dashboardFilters: null,
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("NotAColumn");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPopupColumn_Throws()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        ArgumentException ex = await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                latitudeColumn: "Latitude",
                longitudeColumn: "Longitude",
                popupColumns: ["NotAColumn"],
                dashboardFilters: null,
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("NotAColumn");
    }

    [Fact]
    public async Task ExecuteAsync_DashboardFilter_PassedAsQueryRequestFilter()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine);

        await runner.ExecuteAsync(
            latitudeColumn: "Latitude",
            longitudeColumn: "Longitude",
            popupColumns: null,
            dashboardFilters: new Dictionary<string, string> { ["Country"] = "FR" },
            TestContext.Current.CancellationToken);

        engine.Received(1).ExecuteStreamAsync(
            Arg.Any<IQueryable<TestItem>>(),
            Arg.Is<QueryRequest>(r => r.Filter != null && r.Filter["Country.eq"] == "FR"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Name_IsSetFromConstructor()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        MapRunner<TestItem> runner = new("Granit.Test.Items", new TestItemSource([]), engine);
        runner.Name.ShouldBe("Granit.Test.Items");
    }

    private static IQueryEngine<TestItem> ConfigureStream(IReadOnlyList<TestItem> items)
    {
        IQueryEngine<TestItem> engine = Substitute.For<IQueryEngine<TestItem>>();
        engine.ExecuteStreamAsync(
                Arg.Any<IQueryable<TestItem>>(),
                Arg.Any<QueryRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => YieldAsync(items, default));
        return engine;
    }

    private static async IAsyncEnumerable<TestItem> YieldAsync(
        IReadOnlyList<TestItem> items, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (TestItem item in items)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }

        await Task.CompletedTask;
    }

    public sealed class TestItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? LatitudeNullable { get; set; }
        public double? LongitudeNullable { get; set; }
        public decimal LatitudeDecimal { get; set; }
        public decimal LongitudeDecimal { get; set; }
    }

    public sealed class TestItemSource(IReadOnlyList<TestItem> items) : IQueryableSource<TestItem>
    {
        public IQueryable<TestItem> GetQueryable() => items.AsQueryable();
    }
}
