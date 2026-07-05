// =============================================================================
// Tests - ITenantQueryScope (VULN-001)
// =============================================================================
// The QueryEngine-path counterpart of EfStoreBaseTests. Proves that an
// IQueryableSource<T> routed through ITenantQueryScope.Restrict:
//   1. Fails CLOSED to the host partition on an unsignaled absent tenant
//      (origin=implicit_unsignaled, Warning log) — no foreign-tenant rows.
//   2. Bypasses the multi-tenant filter for a signaled .AllowHostAccess()
//      request (origin=host_endpoint) — all tenants visible.
//   3. Leaves a non-IMultiTenant entity untouched (no metric, no bypass).
//   4. Never bypasses when a tenant is active.
//
// The scope is exercised through its public ITenantQueryScope contract resolved
// from the DI container built by AddGranitPersistence, keeping the concrete
// Internal.TenantQueryScope type (and the EF1001 analyzer) out of the test.
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Diagnostics;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class TenantQueryScopeTests
{
    private static (ITenantQueryScope Scope, IMeterFactory MeterFactory, ServiceProvider Sp) BuildScope(
        bool tenantAvailable,
        bool? hostAccess = null,
        ILoggerProvider? loggerProvider = null)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(tenantAvailable);
        if (tenantAvailable)
        {
            tenant.Id.Returns(Guid.NewGuid());
        }

        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging(b =>
        {
            if (loggerProvider is not null)
            {
                b.AddProvider(loggerProvider);
            }
        });
        services.AddSingleton(tenant);

        if (hostAccess is { } signal)
        {
            IHostAccessContext host = Substitute.For<IHostAccessContext>();
            host.IsHostAccess.Returns(signal);
            services.AddSingleton(host);
        }

        services.AddGranitPersistence();

        ServiceProvider sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<ITenantQueryScope>(), sp.GetRequiredService<IMeterFactory>(), sp);
    }

    [Fact]
    public async Task Restrict_UnsignaledNoTenant_FailsClosed_ReturnsOnlyHostPartition()
    {
        // The core VULN-001 guard: an unsignaled tenant-context loss must NOT widen the query to
        // every tenant. Pre-fix, each IQueryableSource called IgnoreQueryFilters and leaked all rows.
        (ITenantQueryScope scope, _, ServiceProvider sp) = BuildScope(tenantAvailable: false);
        await using ServiceProvider provider = sp;
        var foreignTenant = Guid.NewGuid();
        FilteredTenantDbContextFactory factory = new();
        await SeedAsync(factory, ("host-row", null), ("tenant-row", foreignTenant));

        await using FilteredTenantDbContext db = factory.CreateDbContext();
        IQueryable<TestMultiTenantEntity> query =
            scope.Restrict(db.MultiTenantEntities.AsNoTracking(), nameof(TestMultiTenantEntity));
        List<TestMultiTenantEntity> rows = await query.ToListAsync(TestContext.Current.CancellationToken);

        rows.ShouldHaveSingleItem();
        rows[0].TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task Restrict_HostSignalled_BypassesFilter_ReturnsAllTenants()
    {
        (ITenantQueryScope scope, _, ServiceProvider sp) = BuildScope(tenantAvailable: false, hostAccess: true);
        await using ServiceProvider provider = sp;
        var foreignTenant = Guid.NewGuid();
        FilteredTenantDbContextFactory factory = new();
        await SeedAsync(factory, ("host-row", null), ("tenant-row", foreignTenant));

        await using FilteredTenantDbContext db = factory.CreateDbContext();
        IQueryable<TestMultiTenantEntity> query =
            scope.Restrict(db.MultiTenantEntities.AsNoTracking(), nameof(TestMultiTenantEntity));
        List<TestMultiTenantEntity> rows = await query.ToListAsync(TestContext.Current.CancellationToken);

        rows.Count.ShouldBe(2);
    }

    [Fact]
    public void Restrict_UnsignaledNoTenant_RecordsImplicitUnsignaled_AndLogsWarning()
    {
        using FakeLoggerProvider loggerProvider = new();
        (ITenantQueryScope scope, IMeterFactory meterFactory, ServiceProvider sp) =
            BuildScope(tenantAvailable: false, loggerProvider: loggerProvider);
        using ServiceProvider provider = sp;
        using MetricCollector<long> collector =
            new(meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        _ = scope.Restrict(Array.Empty<TestMultiTenantEntity>().AsQueryable(), nameof(TestMultiTenantEntity));

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["entity"].ShouldBe(nameof(TestMultiTenantEntity));
        snapshot[0].Tags["origin"].ShouldBe("implicit_unsignaled");

        FakeLogRecord record = loggerProvider.Collector.LatestRecord;
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain(nameof(TestMultiTenantEntity));
    }

    [Fact]
    public void Restrict_HostSignalled_RecordsHostEndpoint()
    {
        (ITenantQueryScope scope, IMeterFactory meterFactory, ServiceProvider sp) =
            BuildScope(tenantAvailable: false, hostAccess: true);
        using ServiceProvider provider = sp;
        using MetricCollector<long> collector =
            new(meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        _ = scope.Restrict(Array.Empty<TestMultiTenantEntity>().AsQueryable(), nameof(TestMultiTenantEntity));

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["origin"].ShouldBe("host_endpoint");
    }

    [Fact]
    public void Restrict_WithActiveTenant_DoesNotRecord()
    {
        (ITenantQueryScope scope, IMeterFactory meterFactory, ServiceProvider sp) =
            BuildScope(tenantAvailable: true);
        using ServiceProvider provider = sp;
        using MetricCollector<long> collector =
            new(meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        _ = scope.Restrict(Array.Empty<TestMultiTenantEntity>().AsQueryable(), nameof(TestMultiTenantEntity));

        collector.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Restrict_NonMultiTenantEntity_NeverBypasses_AndDoesNotRecord()
    {
        (ITenantQueryScope scope, IMeterFactory meterFactory, ServiceProvider sp) =
            BuildScope(tenantAvailable: false);
        using ServiceProvider provider = sp;
        using MetricCollector<long> collector =
            new(meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        IQueryable<TestNonTenantEntity> source = Array.Empty<TestNonTenantEntity>().AsQueryable();
        IQueryable<TestNonTenantEntity> result = scope.Restrict(source, nameof(TestNonTenantEntity));

        result.ShouldBeSameAs(source);
        collector.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    private static async Task SeedAsync(
        FilteredTenantDbContextFactory factory,
        params (string Name, Guid? TenantId)[] rows)
    {
        await using FilteredTenantDbContext db = factory.CreateDbContext();
        foreach ((string name, Guid? tenantId) in rows)
        {
            db.MultiTenantEntities.Add(new TestMultiTenantEntity { Id = Guid.NewGuid(), Name = name, TenantId = tenantId });
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // ──── Test fixtures ────

    private sealed class TestMultiTenantEntity : Entity, IMultiTenant
    {
        public Guid? TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestNonTenantEntity : Entity
    {
        public string Name { get; set; } = string.Empty;
    }

    // Wires the named MultiTenant filter parameterized off null (no active tenant), so a
    // non-bypassed query is restricted to the host partition (TenantId == null) — mirrors
    // GranitDbContext's factory-created host context.
    private sealed class FilteredTenantDbContext(DbContextOptions<FilteredTenantDbContext> options)
        : DbContext(options)
    {
        public DbSet<TestMultiTenantEntity> MultiTenantEntities => Set<TestMultiTenantEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<TestMultiTenantEntity>(b =>
            {
                b.Property(e => e.Id).ValueGeneratedNever();
                b.HasQueryFilter(GranitFilterNames.MultiTenant, e => e.TenantId == null);
            });
    }

    private sealed class FilteredTenantDbContextFactory : IDbContextFactory<FilteredTenantDbContext>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        public FilteredTenantDbContext CreateDbContext()
        {
            DbContextOptions<FilteredTenantDbContext> options =
                new DbContextOptionsBuilder<FilteredTenantDbContext>()
                    .UseInMemoryDatabase(_databaseName)
                    .Options;
            return new FilteredTenantDbContext(options);
        }

        public Task<FilteredTenantDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
