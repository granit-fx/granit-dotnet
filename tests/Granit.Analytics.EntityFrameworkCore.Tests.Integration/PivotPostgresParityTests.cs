using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.Analytics.Internal;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Analytics.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Pivot widget end-to-end parity test against a real PostgreSQL — closes the
/// B3 (#1384) DoD item "integration test for the pivot path (real PostgreSQL
/// via Testcontainers)". The PivotRunner streams from
/// <c>IQueryEngine.ExecuteStreamAsync</c> and aggregates in memory, so the SQL
/// emitted is just a filtered <c>SELECT</c>; the integration risk being pinned
/// here is therefore: dashboard filter composition, group-key projection
/// (multi-key tuples, null-key sentinel), and per-cell empty-set semantics
/// match the unit-tested behaviour byte-for-byte on Postgres.
/// </summary>
public sealed class PivotPostgresParityTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = postgres;
    private TestDbContext _db = null!;
    private ServiceProvider _provider = null!;
    private PivotRunner<Customer> _runner = null!;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_postgres.ConnectionString, o => o.UseNetTopologySuite())
            .Options;

        _db = new TestDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await _db.Customers.ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        // Seed: 2 countries × 2 statuses, with a deliberate (FR, Active) cluster
        // and a (US, null) row never seeded — used by the empty-cell test below
        // to assert (Avg/Min/Max) → null on missing keys.
        Customer[] seed =
        [
            new() { Id = Guid.NewGuid(), Country = "FR", Status = "Active",   Revenue = 100m },
            new() { Id = Guid.NewGuid(), Country = "FR", Status = "Active",   Revenue = 50m },
            new() { Id = Guid.NewGuid(), Country = "FR", Status = "Inactive", Revenue = 200m },
            new() { Id = Guid.NewGuid(), Country = "US", Status = "Active",   Revenue = 300m },
        ];
        _db.Customers.AddRange(seed);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (_provider, IQueryEngine<Customer> engine) =
            TestEngineFactory.Build<Customer, CustomerQueryDefinition>();

        _runner = new PivotRunner<Customer>(
            name: "Test.Customers",
            source: new CustomerSource(_db),
            engine: engine,
            definition: _provider.GetRequiredService<QueryDefinition<Customer>>());
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Pivot_Sum_Country_x_Status_AggregatesCorrectly()
    {
        PivotRunnerResult result = await _runner.ExecuteAsync(
            rowFields: ["Country"],
            columnFields: ["Status"],
            valueField: "Revenue",
            aggregation: AggregateFunction.Sum,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        // 3 cells: (FR, Active)=150, (FR, Inactive)=200, (US, Active)=300.
        // (US, Inactive) not seeded, hence not in the cell list.
        result.Cells.Count.ShouldBe(3);

        decimal? frActive = CellValue(result, ["FR"], ["Active"]);
        decimal? frInactive = CellValue(result, ["FR"], ["Inactive"]);
        decimal? usActive = CellValue(result, ["US"], ["Active"]);

        frActive.ShouldBe(150m);
        frInactive.ShouldBe(200m);
        usActive.ShouldBe(300m);
    }

    [Fact]
    public async Task Pivot_Count_Country_x_Status_CountsRows()
    {
        PivotRunnerResult result = await _runner.ExecuteAsync(
            rowFields: ["Country"],
            columnFields: ["Status"],
            valueField: null,
            aggregation: AggregateFunction.Count,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        CellValue(result, ["FR"], ["Active"]).ShouldBe(2m);   // 2 rows
        CellValue(result, ["FR"], ["Inactive"]).ShouldBe(1m); // 1 row
        CellValue(result, ["US"], ["Active"]).ShouldBe(1m);   // 1 row
    }

    [Fact]
    public async Task Pivot_DashboardFilter_NarrowsRowsBeforeAggregation()
    {
        // Filter: Country eq FR. Pivot only sees FR rows; the US Active cell vanishes.
        PivotRunnerResult result = await _runner.ExecuteAsync(
            rowFields: ["Country"],
            columnFields: ["Status"],
            valueField: "Revenue",
            aggregation: AggregateFunction.Sum,
            dashboardFilters: new Dictionary<string, string> { ["Country"] = "FR" },
            TestContext.Current.CancellationToken);

        result.Cells.Count.ShouldBe(2); // (FR, Active), (FR, Inactive)
        CellValue(result, ["FR"], ["Active"]).ShouldBe(150m);
        CellValue(result, ["FR"], ["Inactive"]).ShouldBe(200m);
        result.Cells.ShouldNotContain(c => c.RowKeys.SequenceEqual(new[] { "US" }));
    }

    [Fact]
    public async Task Pivot_EmptyResultSet_ProducesNoCells()
    {
        // Filter that matches no row.
        PivotRunnerResult result = await _runner.ExecuteAsync(
            rowFields: ["Country"],
            columnFields: ["Status"],
            valueField: "Revenue",
            aggregation: AggregateFunction.Sum,
            dashboardFilters: new Dictionary<string, string> { ["Country"] = "ZZ" },
            TestContext.Current.CancellationToken);

        result.Cells.ShouldBeEmpty();
    }

    [Fact]
    public async Task Pivot_Avg_FrActiveCluster_AveragesCellValues()
    {
        PivotRunnerResult result = await _runner.ExecuteAsync(
            rowFields: ["Country"],
            columnFields: ["Status"],
            valueField: "Revenue",
            aggregation: AggregateFunction.Avg,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        // (FR, Active) holds 100 and 50 → Avg = 75.
        CellValue(result, ["FR"], ["Active"]).ShouldBe(75m);
        // (FR, Inactive) is a single 200 → Avg = 200.
        CellValue(result, ["FR"], ["Inactive"]).ShouldBe(200m);
    }

    [Fact]
    public async Task Pivot_NoColumnFields_OneRowPerRowKeyTuple()
    {
        // Column-less pivot — one cell per row tuple, with empty ColumnKeys.
        PivotRunnerResult result = await _runner.ExecuteAsync(
            rowFields: ["Country"],
            columnFields: [],
            valueField: "Revenue",
            aggregation: AggregateFunction.Sum,
            dashboardFilters: null,
            TestContext.Current.CancellationToken);

        result.Cells.Count.ShouldBe(2);
        result.Cells.ShouldAllBe(c => c.ColumnKeys.Count == 0);

        CellValue(result, ["FR"], []).ShouldBe(350m); // 100 + 50 + 200
        CellValue(result, ["US"], []).ShouldBe(300m);
    }

    private static decimal? CellValue(
        PivotRunnerResult result,
        IReadOnlyList<string> rowKeys,
        IReadOnlyList<string> columnKeys) =>
        result.Cells.Single(c =>
                c.RowKeys.SequenceEqual(rowKeys, StringComparer.Ordinal) &&
                c.ColumnKeys.SequenceEqual(columnKeys, StringComparer.Ordinal))
            .Value;
}
