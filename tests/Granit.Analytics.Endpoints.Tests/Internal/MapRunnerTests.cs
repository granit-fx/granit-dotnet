using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.EntityFrameworkCore.Diagnostics;
using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.Analytics.Internal;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
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
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine, BuildMetrics());

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
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
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine, BuildMetrics());

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: new MapPointSource.LatLng("LatitudeNullable", "LongitudeNullable"),
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
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine, BuildMetrics());

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
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
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine, BuildMetrics());

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
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
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine, BuildMetrics());

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
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
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine, BuildMetrics());

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: new MapPointSource.LatLng("LatitudeDecimal", "LongitudeDecimal"),
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
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine, BuildMetrics());

        await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                pointSource: new MapPointSource.LatLng("Name", "Longitude"), // "Name" is a string column
                popupColumns: null,
                dashboardFilters: null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownLatitudeColumn_Throws()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine, BuildMetrics());

        ArgumentException ex = await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                pointSource: new MapPointSource.LatLng("NotAColumn", "Longitude"),
                popupColumns: null,
                dashboardFilters: null,
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("NotAColumn");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPopupColumn_Throws()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine, BuildMetrics());

        ArgumentException ex = await Should.ThrowAsync<ArgumentException>(async () =>
            await runner.ExecuteAsync(
                pointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
                popupColumns: ["NotAColumn"],
                dashboardFilters: null,
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("NotAColumn");
    }

    [Fact]
    public async Task ExecuteAsync_DashboardFilter_PassedAsQueryRequestFilter()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine, BuildMetrics());

        await runner.ExecuteAsync(
            pointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
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
        MapRunner<TestItem> runner = new("Granit.Test.Items", new TestItemSource([]), engine, BuildMetrics());
        runner.Name.ShouldBe("Granit.Test.Items");
    }

    [Theory]
    [InlineData(91d, 0d, "latitude_out_of_range")]
    [InlineData(-91d, 0d, "latitude_out_of_range")]
    [InlineData(0d, 181d, "longitude_out_of_range")]
    [InlineData(0d, -181d, "longitude_out_of_range")]
    [InlineData(double.NaN, 0d, "non_finite")]
    [InlineData(0d, double.PositiveInfinity, "non_finite")]
    public async Task ExecuteAsync_InvalidCoordinates_RowSkipped_AndCounterIncrements(
        double lat, double lng, string expectedReason)
    {
        TestItem[] items =
        [
            new() { Id = Guid.NewGuid(), Name = "Bad row", Latitude = lat, Longitude = lng },
            new() { Id = Guid.NewGuid(), Name = "Good row", Latitude = 48.85, Longitude = 2.35 },
        ];

        using var scope = new MetricsScope();
        using MetricCollector<long> collector = new(
            scope.MeterFactory, AnalyticsRuntimeMetrics.MeterName, "granit.analytics.map.invalid_coordinates");

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine, scope.Metrics);

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        // Bad row dropped, good row kept — the widget keeps rendering.
        result.Points.Count.ShouldBe(1);
        result.Points[0].Latitude.ShouldBe(48.85);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["reason"].ShouldBe(expectedReason);
        // No tenant context wired here — falls back to "global".
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public async Task ExecuteAsync_ValidCoordinatesAtBoundaries_DoNotIncrementInvalidCounter()
    {
        TestItem[] items =
        [
            new() { Id = Guid.NewGuid(), Latitude = 0d, Longitude = 0d },           // null island
            new() { Id = Guid.NewGuid(), Latitude = 90d, Longitude = 180d },        // upper boundary
            new() { Id = Guid.NewGuid(), Latitude = -90d, Longitude = -180d },      // lower boundary
        ];

        using var scope = new MetricsScope();
        using MetricCollector<long> collector = new(
            scope.MeterFactory, AnalyticsRuntimeMetrics.MeterName, "granit.analytics.map.invalid_coordinates");

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine, scope.Metrics);

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Points.Count.ShouldBe(3);
        collector.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task SupportsGeography_FalseWhenNoProjector_TrueWhenInjected()
    {
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        MapRunner<TestItem> runnerLatLngOnly = new("Test.Items", new TestItemSource([]), engine, BuildMetrics());
        runnerLatLngOnly.SupportsGeography.ShouldBeFalse();

        IGeographyPointProjector<TestItem> projector = Substitute.For<IGeographyPointProjector<TestItem>>();
        MapRunner<TestItem> runnerWithGeography = new(
            "Test.Items", new TestItemSource([]), engine, BuildMetrics(),
            currentTenant: null, geographyProjector: projector);
        runnerWithGeography.SupportsGeography.ShouldBeTrue();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_GeographyPointSource_DelegatesToProjector()
    {
        TestItem[] items =
        [
            new() { Id = Guid.NewGuid(), Name = "Has point" },
            new() { Id = Guid.NewGuid(), Name = "Null point" },
        ];

        IGeographyPointProjector<TestItem> projector = Substitute.For<IGeographyPointProjector<TestItem>>();
        projector.TryProject(items[0], "Position").Returns((Latitude: 48.85, Longitude: 2.35));
        projector.TryProject(items[1], "Position").Returns((null as (double, double)?));

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new(
            "Test.Items", new TestItemSource(items), engine, BuildMetrics(),
            currentTenant: null, geographyProjector: projector);

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: new MapPointSource.Geography("Position"),
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        // Null-point row dropped silently (mirrors null lat/lng behaviour).
        result.Points.Count.ShouldBe(1);
        result.Points[0].Latitude.ShouldBe(48.85);
        result.Points[0].Longitude.ShouldBe(2.35);
    }

    [Fact]
    public async Task ExecuteAsync_GeographyPointSource_NoProjector_Throws()
    {
        // Renderer is supposed to pre-check SupportsGeography — this throw is
        // a safety net for misconfigured callers, not the user-facing path.
        IQueryEngine<TestItem> engine = ConfigureStream([]);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource([]), engine, BuildMetrics());

        await Should.ThrowAsync<NotSupportedException>(async () =>
            await runner.ExecuteAsync(
                pointSource: new MapPointSource.Geography("Position"),
                popupColumns: null,
                dashboardFilters: null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_GeographyPointSource_InvalidCoordinates_DroppedAndCounted()
    {
        // Coordinate validation runs uniformly across both paths — the
        // projector returns the (lat, lng) and the runner applies WGS84
        // bounds + finiteness checks. Counter increments with the same
        // reason tags as the LatLng path.
        TestItem[] items = [new() { Id = Guid.NewGuid(), Name = "OutOfRange" }];

        IGeographyPointProjector<TestItem> projector = Substitute.For<IGeographyPointProjector<TestItem>>();
        projector.TryProject(items[0], "Position").Returns((Latitude: 999d, Longitude: 0d));

        using var scope = new MetricsScope();
        using MetricCollector<long> collector = new(
            scope.MeterFactory, AnalyticsRuntimeMetrics.MeterName, "granit.analytics.map.invalid_coordinates");

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new(
            "Test.Items", new TestItemSource(items), engine, scope.Metrics,
            currentTenant: null, geographyProjector: projector);

        MapRunnerResult result = await runner.ExecuteAsync(
            pointSource: new MapPointSource.Geography("Position"),
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Points.ShouldBeEmpty();
        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["reason"].ShouldBe("latitude_out_of_range");
    }

    [Fact]
    public async Task ExecuteAsync_TenantContextAvailable_TagsCounterWithTenantId()
    {
        var tenantId = Guid.NewGuid();
        TestItem[] items = [new() { Id = Guid.NewGuid(), Latitude = 999d, Longitude = 0d }];

        using var scope = new MetricsScope();
        using MetricCollector<long> collector = new(
            scope.MeterFactory, AnalyticsRuntimeMetrics.MeterName, "granit.analytics.map.invalid_coordinates");

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(tenantId);

        IQueryEngine<TestItem> engine = ConfigureStream(items);
        MapRunner<TestItem> runner = new("Test.Items", new TestItemSource(items), engine, scope.Metrics, currentTenant);

        await runner.ExecuteAsync(
            pointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe(tenantId.ToString());
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

    /// <summary>
    /// Builds a throwaway <see cref="AnalyticsRuntimeMetrics"/> for tests that don't
    /// inspect the counter — keeps the existing happy-path tests focused on projection
    /// without leaking metric setup boilerplate.
    /// </summary>
    private static AnalyticsRuntimeMetrics BuildMetrics()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider sp = services.BuildServiceProvider();
        return new AnalyticsRuntimeMetrics(sp.GetRequiredService<IMeterFactory>());
    }

    /// <summary>
    /// Disposable harness — owns a <see cref="ServiceProvider"/> + <see cref="IMeterFactory"/>
    /// and exposes the matching <see cref="AnalyticsRuntimeMetrics"/>. The metric collector
    /// must be created from the SAME factory that built the metrics instance, otherwise
    /// the snapshot will be empty.
    /// </summary>
    private sealed class MetricsScope : IDisposable
    {
        private readonly ServiceProvider _sp;
        public IMeterFactory MeterFactory { get; }
        public AnalyticsRuntimeMetrics Metrics { get; }

        public MetricsScope()
        {
            ServiceCollection services = new();
            services.AddMetrics();
            _sp = services.BuildServiceProvider();
            MeterFactory = _sp.GetRequiredService<IMeterFactory>();
            Metrics = new AnalyticsRuntimeMetrics(MeterFactory);
        }

        public void Dispose() => _sp.Dispose();
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
