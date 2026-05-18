// =============================================================================
// Regression — multi-tenant query filter must remain parameterised
// =============================================================================
// Locks the fix delivered by introducing the `GranitDbContext` base class.
// The previous extension-method form built the IMultiTenant filter via
// `Expression.Property(Expression.Constant(currentTenant), "Id")`, which EF
// Core inlined as a literal into the compiled SQL — the model cache then
// reused that frozen value across every subsequent request of the same
// DbContext type, leaking tenant A's rows to tenant B (and to anonymous
// requests). Captured SQL with the bug:
//
//   SELECT ... FROM "Items" AS "i" WHERE "i"."TenantId" = '<guid-literal>'
//
// The fix moves the filter expression inside a member of `GranitDbContext`,
// where `this.CurrentTenantId` is recognised by EF Core's parameter
// extractor and emitted as `@ef_filter__CurrentTenantId`. These tests
// assert that contract end-to-end against SQLite (real query translation)
// using DISTINCT DbContext types per test so each model is built fresh and
// the per-type model cache cannot mask a regression.
// =============================================================================

using System.Globalization;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

        // EF Core emits the filter parameter as "@ef_filter__<DbContextProperty>"
        // (or any "@param"-shaped placeholder) in the WHERE clause and lists the
        // bound value in the Parameters=[...] prefix. The literal GUID appears
        // ONLY in the Parameters list — never in the SQL body. Assert both signals
        // independently to defeat any future change in EF's logging format.
        bool hasParameterPlaceholder = selectSql.Contains(
            "\"i\".\"TenantId\" = @",
            StringComparison.Ordinal);

        hasParameterPlaceholder.ShouldBeTrue(
            $"tenant filter must compile to a parameter, not a literal GUID.\nSQL:\n{selectSql}");

        string whereLine = selectSql
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(l => l.Contains("WHERE", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("No WHERE clause in captured SQL.");

        whereLine.Contains(
            tenantA.ToString("D", CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
            $"WHERE clause must not inline the tenant GUID.\nWHERE:\n{whereLine}");
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

    // All 3 repro contexts inherit from GranitDbContext: the IMultiTenant filter is then
    // built inside a member of the DbContext type, where `this.CurrentTenantId` is
    // recognised by EF Core's parameter extractor.
    private sealed class ReproDbContextA(DbContextOptions<ReproDbContextA> options, ICurrentTenant tenant)
        : GranitDbContext(options, tenant)
    {
        public DbSet<TenantItem> Items => Set<TenantItem>();
    }

    private sealed class ReproDbContextB(DbContextOptions<ReproDbContextB> options, ICurrentTenant tenant)
        : GranitDbContext(options, tenant)
    {
        public DbSet<TenantItem> Items => Set<TenantItem>();
    }

    private sealed class ReproDbContextC(DbContextOptions<ReproDbContextC> options, ICurrentTenant tenant)
        : GranitDbContext(options, tenant)
    {
        public DbSet<TenantItem> Items => Set<TenantItem>();
    }
}
