// =============================================================================
// Repro — multi-tenant query filter "frozen tenant" hypothesis
// =============================================================================
// Investigates the diagnosis recorded on the [Fact(Skip = ...)] of
// `tests/Granit.Http.ODataExposure.Tests.Integration/TenantIsolationTests.cs`
// (UnauthenticatedRequest_NoTenantHeader_ReturnsEmpty):
//
//   "EF Core inlines currentTenant.Id into the compiled SQL at first model
//    build instead of parameterising, so subsequent requests reuse the
//    FROZEN tenant value (captured SQL: WHERE TenantId = '<frozen-guid>')."
//
// Existing tests in ModelBuilderExtensionsTests.cs verify the filter lambda
// re-evaluates the closure, but they (a) compile the LambdaExpression directly
// and (b) use the InMemory provider — both paths bypass relational query
// translation, which is where the alleged "constant folding" would happen.
//
// These tests use SQLite (real relational translation) and capture the
// generated SQL via `LogTo` to settle the question empirically. Each test
// uses a DISTINCT DbContext type to defeat EF Core's per-type model cache:
// the cache is the suspected vehicle for the "frozen tenant" leak.
// =============================================================================

using System.Globalization;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class MultiTenantFilterParameterizationReproTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private readonly List<string> _sqlLog = [];

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private const string SkipReason =
        "Documents the known bug mirrored by " +
        "Granit.Http.ODataExposure.Tests.Integration/TenantIsolationTests.cs:191 " +
        "(UnauthenticatedRequest_NoTenantHeader_ReturnsEmpty). ApplyGranitConventions " +
        "builds the multi-tenant filter via Expression.Property(Expression.Constant(currentTenant), \"Id\"), " +
        "which EF Core inlines as a literal into the compiled SQL (verified: " +
        "WHERE \"TenantId\" = '<guid-literal>', no parameter). The model cache then " +
        "reuses that frozen value for every subsequent query of the same DbContext type. " +
        "Re-enable once ModelBuilderExtensions.cs (the IMultiTenant block) is reworked " +
        "to a form EF Core parameterises (candidate: C# lambda with closure capture).";

    [Fact(Skip = SkipReason)]
    public async Task SqlFilter_ReEvaluatesTenantAcross_DifferentDbContextInstances_SameType()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        MutableTenant tenant = new() { Id = tenantA };

        await SeedReproAAsync(tenant, tenantA, tenantB, ct);

        List<int> ids1;
        await using (ReproDbContextA ctx1 = new(BuildOpts<ReproDbContextA>(), tenant))
        {
            ids1 = await ctx1.Items.Select(e => e.Id).ToListAsync(ct);
        }

        tenant.Id = tenantB;
        List<int> ids2;
        await using (ReproDbContextA ctx2 = new(BuildOpts<ReproDbContextA>(), tenant))
        {
            ids2 = await ctx2.Items.Select(e => e.Id).ToListAsync(ct);
        }

        DumpSql();

        ids1.Count.ShouldBe(1, "tenant A query should return tenant A's row");
        ids2.Count.ShouldBe(1, "tenant B query should return tenant B's row");
        ids1[0].ShouldNotBe(ids2[0], "the two queries must see different rows");
    }

    [Fact(Skip = SkipReason)]
    public async Task SqlFilter_ReEvaluatesWhenTenantBecomesNull_AfterFirstQuery()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        MutableTenant tenant = new() { Id = tenantA };

        await SeedReproBAsync(tenant, tenantA, tenantB, ct);

        await using (ReproDbContextB ctx1 = new(BuildOpts<ReproDbContextB>(), tenant))
        {
            (await ctx1.Items.CountAsync(ct)).ShouldBe(1, "tenant A should see 1 row");
        }

        tenant.Id = null;
        await using (ReproDbContextB ctx2 = new(BuildOpts<ReproDbContextB>(), tenant))
        {
            int count = await ctx2.Items.CountAsync(ct);
            DumpSql();
            count.ShouldBe(0, $"anonymous request must see no rows, got {count}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task SqlFilter_ShouldParameterize_NotInlineTenantConstant()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        MutableTenant tenant = new() { Id = tenantA };

        await SeedReproCAsync(tenant, tenantA, tenantB, ct);

        await using (ReproDbContextC ctx = new(BuildOpts<ReproDbContextC>(), tenant))
        {
            _ = await ctx.Items.ToListAsync(ct);
        }

        DumpSql();

        string selectSql = _sqlLog
            .FirstOrDefault(s => s.Contains("SELECT", StringComparison.OrdinalIgnoreCase)
                              && s.Contains("Items", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No SELECT against Items captured.");

        bool inlined = selectSql.Contains(
            tenantA.ToString("D", CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);

        inlined.ShouldBeFalse(
            $"tenant GUID is inlined into the compiled SQL (frozen value).\nSQL:\n{selectSql}");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private DbContextOptions<TCtx> BuildOpts<TCtx>() where TCtx : DbContext
        => new DbContextOptionsBuilder<TCtx>()
            .UseSqlite(_connection)
            .LogTo(line => _sqlLog.Add(line), LogLevel.Information)
            .EnableSensitiveDataLogging()
            .Options;

    private async Task SeedReproAAsync(MutableTenant tenant, Guid tenantA, Guid tenantB, CancellationToken ct)
    {
        tenant.Id = tenantA;
        await using ReproDbContextA seed = new(BuildOpts<ReproDbContextA>(), tenant);
        await seed.Database.EnsureCreatedAsync(ct);
        seed.Items.Add(new TenantItem { TenantId = tenantA, Label = "A" });
        seed.Items.Add(new TenantItem { TenantId = tenantB, Label = "B" });
        await seed.SaveChangesAsync(ct);
        _sqlLog.Clear();
        tenant.Id = tenantA;
    }

    private async Task SeedReproBAsync(MutableTenant tenant, Guid tenantA, Guid tenantB, CancellationToken ct)
    {
        tenant.Id = tenantA;
        await using ReproDbContextB seed = new(BuildOpts<ReproDbContextB>(), tenant);
        await seed.Database.EnsureCreatedAsync(ct);
        seed.Items.Add(new TenantItem { TenantId = tenantA, Label = "A" });
        seed.Items.Add(new TenantItem { TenantId = tenantB, Label = "B" });
        await seed.SaveChangesAsync(ct);
        _sqlLog.Clear();
        tenant.Id = tenantA;
    }

    private async Task SeedReproCAsync(MutableTenant tenant, Guid tenantA, Guid tenantB, CancellationToken ct)
    {
        tenant.Id = tenantA;
        await using ReproDbContextC seed = new(BuildOpts<ReproDbContextC>(), tenant);
        await seed.Database.EnsureCreatedAsync(ct);
        seed.Items.Add(new TenantItem { TenantId = tenantA, Label = "A" });
        seed.Items.Add(new TenantItem { TenantId = tenantB, Label = "B" });
        await seed.SaveChangesAsync(ct);
        _sqlLog.Clear();
        tenant.Id = tenantA;
    }

    private void DumpSql()
    {
        foreach (string line in _sqlLog)
        {
            Console.WriteLine(line);
        }
    }

    private sealed class MutableTenant : ICurrentTenant
    {
        public bool IsAvailable => Id.HasValue;
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public IDisposable Change(Guid? id, string? name = null)
            => throw new NotSupportedException();
    }

    public sealed class TenantItem : IMultiTenant
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
    }

    private sealed class ReproDbContextA(DbContextOptions<ReproDbContextA> options, ICurrentTenant tenant)
        : DbContext(options)
    {
        public DbSet<TenantItem> Items => Set<TenantItem>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyGranitConventions(tenant);
    }

    private sealed class ReproDbContextB(DbContextOptions<ReproDbContextB> options, ICurrentTenant tenant)
        : DbContext(options)
    {
        public DbSet<TenantItem> Items => Set<TenantItem>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyGranitConventions(tenant);
    }

    private sealed class ReproDbContextC(DbContextOptions<ReproDbContextC> options, ICurrentTenant tenant)
        : DbContext(options)
    {
        public DbSet<TenantItem> Items => Set<TenantItem>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyGranitConventions(tenant);
    }
}
