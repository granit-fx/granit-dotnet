using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.Endpoints.Diagnostics;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.PostGIS.Internal;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Shouldly;
using Xunit;

namespace Granit.Analytics.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// End-to-end test for B8 (#1568) — a <c>geography(Point)</c> column on a real
/// PostGIS database, read by Npgsql's NetTopologySuite plugin, projected to
/// lat/lng by <c>NtsGeographyPointProjector</c>, streamed through the
/// QueryEngine pipeline by <c>MapRunner&lt;Branch&gt;</c>. Pinning the WKT
/// axis order (X = lng, Y = lat) and the null-Point silent-skip behaviour on
/// the actual database engine, not just the unit-tested projector.
/// </summary>
public sealed class MapPostGisIntegrationTests(PostGisFixture postgis)
    : IClassFixture<PostGisFixture>, IAsyncLifetime
{
    private readonly PostGisFixture _postgis = postgis;
    private TestDbContext _db = null!;
    private ServiceProvider _provider = null!;
    private MapRunner<Branch> _runner = null!;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_postgis.ConnectionString, o => o.UseNetTopologySuite())
            .Options;

        _db = new TestDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await _db.Branches.ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        // Seed: Paris + London with a Point each, plus a row with null Location
        // to assert the silent-skip path.
        Branch[] seed =
        [
            new() { Id = Guid.NewGuid(), Name = "Paris HQ", Location = new Point(x: 2.3522, y: 48.8566) { SRID = 4326 } },
            new() { Id = Guid.NewGuid(), Name = "London Office", Location = new Point(x: -0.1276, y: 51.5074) { SRID = 4326 } },
            new() { Id = Guid.NewGuid(), Name = "Pending Office", Location = null },
        ];
        _db.Branches.AddRange(seed);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (_provider, IQueryEngine<Branch> engine) =
            TestEngineFactory.Build<Branch, BranchQueryDefinition>();

        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider metricsProvider = services.BuildServiceProvider();
        AnalyticsEndpointsMetrics metrics = new(metricsProvider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());

        _runner = new MapRunner<Branch>(
            name: "Test.Branches",
            source: new BranchSource(_db),
            engine: engine,
            metrics: metrics,
            currentTenant: null,
            geographyProjector: new NtsGeographyPointProjector<Branch>());
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Geography_ReadsPointColumn_ProjectsToWgs84LatLng()
    {
        _runner.SupportsGeography.ShouldBeTrue();

        MapRunnerResult result = await _runner.ExecuteAsync(
            pointSource: new MapPointSource.Geography("Location"),
            popupColumns: ["Name"],
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        // Two points seeded with non-null Location, third row dropped.
        result.Points.Count.ShouldBe(2);

        MapRunnerPoint paris = result.Points.Single(p =>
            Math.Abs(p.Latitude - 48.8566) < 0.0001 && Math.Abs(p.Longitude - 2.3522) < 0.0001);
        paris.Popup.ShouldNotBeNull();
        paris.Popup!.Value.GetProperty("name").GetString().ShouldBe("Paris HQ");

        MapRunnerPoint london = result.Points.Single(p =>
            Math.Abs(p.Latitude - 51.5074) < 0.0001 && Math.Abs(p.Longitude - (-0.1276)) < 0.0001);
        london.Popup!.Value.GetProperty("name").GetString().ShouldBe("London Office");
    }

    [Fact]
    public async Task Geography_NullPointRow_SilentlySkipped()
    {
        // Already covered as a side-effect by the test above (3 seeded, 2 returned),
        // but keeping the explicit assertion close to the contract — null Point on
        // any row MUST drop, not throw.
        MapRunnerResult result = await _runner.ExecuteAsync(
            pointSource: new MapPointSource.Geography("Location"),
            popupColumns: null,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Points.Count.ShouldBe(2);
        result.Points.ShouldAllBe(p => p.Latitude >= -90 && p.Latitude <= 90);
        result.Points.ShouldAllBe(p => p.Longitude >= -180 && p.Longitude <= 180);
    }
}
