// =============================================================================
// Tests - EfStoreBase
// =============================================================================
// Cover the cross-tenant filter behavior:
//   1. Per-call evaluation (no constructor-time caching) — closes the edge
//      case where a Scoped store was constructed before the tenant was activated
//      and stayed in cross-tenant mode for the rest of the scope.
//   2. Behavior when ICurrentTenant.IsAvailable=false — split by IHostAccessContext:
//        - host-access signaled (via .AllowHostAccess()) → filter BYPASSED, origin=host_endpoint
//        - no signal → FAIL CLOSED (filter kept, host partition only),
//          origin=implicit_unsignaled (alert-worthy: tenant-context loss)
//   3. Explicit QueryAcrossTenants() → origin=explicit (caller opt-in).
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class EfStoreBaseTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly PersistenceMetrics _metrics;
    private readonly TestDbContextFactory _contextFactory;

    public EfStoreBaseTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new PersistenceMetrics(_meterFactory);
        _contextFactory = new TestDbContextFactory();
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task Query_WithActiveTenant_DoesNotRecord_CrossTenantQuery()
    {
        // Sanity: the happy path (active tenant) must not emit any
        // cross-tenant metric. Anything else would create alert noise.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(Guid.NewGuid());
        TestStore store = new(_contextFactory, tenant, hostAccess: null, _metrics);
        using MetricCollector<long> collector = new(_meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        await using TestDbContext db = await _contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        _ = store.QueryEntities(db);

        collector.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task Query_WithoutActiveTenant_NoSignal_RecordsImplicitUnsignaled()
    {
        // No tenant + no host-access signal = the alert-worthy origin. SOC alerts
        // on this counter > 0 to detect tenant-context loss in flight.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        TestStore store = new(_contextFactory, tenant, hostAccess: null, _metrics);
        using MetricCollector<long> collector = new(_meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        await using TestDbContext db = await _contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        _ = store.QueryEntities(db);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["entity"].ShouldBe(nameof(TestMultiTenantEntity));
        snapshot[0].Tags["origin"].ShouldBe("implicit_unsignaled");
    }

    [Fact]
    public async Task Query_WithoutActiveTenant_HostSignalled_RecordsHostEndpoint()
    {
        // No tenant + host-access signal = expected .AllowHostAccess() path.
        // Distinct origin so SOC can suppress this from leak alerts.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        IHostAccessContext hostAccess = Substitute.For<IHostAccessContext>();
        hostAccess.IsHostAccess.Returns(true);
        TestStore store = new(_contextFactory, tenant, hostAccess, _metrics);
        using MetricCollector<long> collector = new(_meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        await using TestDbContext db = await _contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        _ = store.QueryEntities(db);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["origin"].ShouldBe("host_endpoint");
    }

    [Fact]
    public async Task Query_TenantBecomesAvailableAfterConstruction_NoBypass()
    {
        // Root-cause check: the previous implementation cached the bypass
        // decision at construction. A Scoped store created before the tenant
        // was activated kept bypassing for the rest of the scope. With
        // per-call evaluation, this no longer happens.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        TestStore store = new(_contextFactory, tenant, hostAccess: null, _metrics);
        using MetricCollector<long> collector = new(_meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        // Tenant becomes available after the store was created.
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(Guid.NewGuid());

        await using TestDbContext db = await _contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        _ = store.QueryEntities(db);

        collector.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task QueryAcrossTenants_RecordsExplicitBypass()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(Guid.NewGuid());
        TestStore store = new(_contextFactory, tenant, hostAccess: null, _metrics);
        using MetricCollector<long> collector = new(_meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        await using TestDbContext db = await _contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        _ = store.QueryEntitiesAcrossTenants(db);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["entity"].ShouldBe(nameof(TestMultiTenantEntity));
        snapshot[0].Tags["origin"].ShouldBe("explicit");
    }

    [Fact]
    public async Task Query_UnsignaledBypass_LogsWarning()
    {
        // Unsignaled bypass logs a Warning so the SOC can correlate the metric tick
        // with a searchable trace pointing at the entity.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        FakeLogger logger = new();
        TestStore store = new(_contextFactory, tenant, hostAccess: null, _metrics, logger);

        await using TestDbContext db = await _contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        _ = store.QueryEntities(db);

        FakeLogRecord record = logger.LatestRecord;
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain(nameof(TestMultiTenantEntity));
    }

    [Fact]
    public async Task QueryAcrossTenants_LogsInformation_WithCallerFrame()
    {
        // Explicit opt-in logs Information with the caller member/file/line so an
        // operator can pinpoint which call-site triggered the cross-tenant read
        // without spelunking through stack traces.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(Guid.NewGuid());
        FakeLogger logger = new();
        TestStore store = new(_contextFactory, tenant, hostAccess: null, _metrics, logger);

        await using TestDbContext db = await _contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        _ = store.QueryEntitiesAcrossTenants(db);

        FakeLogRecord record = logger.LatestRecord;
        record.Level.ShouldBe(LogLevel.Information);
        record.Message.ShouldContain(nameof(TestStore.QueryEntitiesAcrossTenants));
        record.Message.ShouldContain(nameof(EfStoreBaseTests));
    }

    [Fact]
    public async Task Query_NonMultiTenantEntity_NeverBypasses()
    {
        // EfStoreBase only triggers the bypass when TEntity implements IMultiTenant.
        // Non-tenant entities are always served unfiltered (the named filter does
        // not exist for them) — and must not emit the metric.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        TestNonTenantStore store = new(_contextFactory, tenant, _metrics);
        using MetricCollector<long> collector = new(_meterFactory, PersistenceMetrics.MeterName, "granit.persistence.cross_tenant_query");

        await using TestDbContext db = await _contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        _ = store.QueryEntities(db);

        collector.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task Query_UnsignaledNoTenant_FailsClosed_ReturnsOnlyHostPartition()
    {
        // The core VULN-200 guard: an unsignaled tenant-context loss must NOT widen the query
        // to every tenant. With the named MultiTenant filter still in place and no active tenant
        // (filter value == null), only the host partition (TenantId == null) is visible. The
        // foreign-tenant row stays hidden — pre-fix this branch called IgnoreQueryFilters and
        // returned both rows.
        var foreignTenant = Guid.NewGuid();
        FilteredTenantDbContextFactory factory = new(filterTenantId: null);
        await SeedAsync(factory, ("host-row", null), ("tenant-row", foreignTenant));

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        FilteredTenantStore store = new(factory, tenant, hostAccess: null, _metrics);

        await using FilteredTenantDbContext db = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        List<TestMultiTenantEntity> rows = await store.QueryEntities(db).ToListAsync(TestContext.Current.CancellationToken);

        rows.ShouldHaveSingleItem();
        rows[0].TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task Query_HostSignalled_BypassesFilter_ReturnsAllTenants()
    {
        // The signaled host route (.AllowHostAccess()) is the authorized cross-tenant path:
        // the filter is bypassed and every tenant's rows are visible.
        var foreignTenant = Guid.NewGuid();
        FilteredTenantDbContextFactory factory = new(filterTenantId: null);
        await SeedAsync(factory, ("host-row", null), ("tenant-row", foreignTenant));

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        IHostAccessContext hostAccess = Substitute.For<IHostAccessContext>();
        hostAccess.IsHostAccess.Returns(true);
        FilteredTenantStore store = new(factory, tenant, hostAccess, _metrics);

        await using FilteredTenantDbContext db = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        List<TestMultiTenantEntity> rows = await store.QueryEntities(db).ToListAsync(TestContext.Current.CancellationToken);

        rows.Count.ShouldBe(2);
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

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestMultiTenantEntity> MultiTenantEntities => Set<TestMultiTenantEntity>();
        public DbSet<TestNonTenantEntity> NonTenantEntities => Set<TestNonTenantEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestMultiTenantEntity>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<TestNonTenantEntity>().Property(e => e.Id).ValueGeneratedNever();
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<TestDbContext>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        public TestDbContext CreateDbContext()
        {
            DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;
            return new TestDbContext(options);
        }

        public Task<TestDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    /// <summary>
    /// Test concrete subclass exposing the protected <c>Query</c> /
    /// <c>QueryAcrossTenants</c> for direct assertion.
    /// </summary>
    private sealed class TestStore(
        IDbContextFactory<TestDbContext> contextFactory,
        ICurrentTenant currentTenant,
        IHostAccessContext? hostAccess,
        PersistenceMetrics metrics,
        ILogger? logger = null)
        : EfStoreBase<TestMultiTenantEntity, TestDbContext>(contextFactory, currentTenant, hostAccess, metrics, logger)
    {
        public IQueryable<TestMultiTenantEntity> QueryEntities(TestDbContext db) => Query(db);

        public IQueryable<TestMultiTenantEntity> QueryEntitiesAcrossTenants(TestDbContext db) => QueryAcrossTenants(db);
    }

    private sealed class TestNonTenantStore(
        IDbContextFactory<TestDbContext> contextFactory,
        ICurrentTenant currentTenant,
        PersistenceMetrics metrics)
        : EfStoreBase<TestNonTenantEntity, TestDbContext>(contextFactory, currentTenant, hostAccess: null, metrics)
    {
        public IQueryable<TestNonTenantEntity> QueryEntities(TestDbContext db) => Query(db);
    }

    // A context that actually wires the named MultiTenant query filter, parameterized off an
    // instance field (mirrors GranitDbContext). Lets the behavioral fail-closed tests observe
    // row visibility, not just metrics. The filter value is null when no tenant is active, so a
    // non-bypassed query is restricted to the host partition (TenantId == null).
    private sealed class FilteredTenantDbContext(
        DbContextOptions<FilteredTenantDbContext> options,
        Guid? filterTenantId) : DbContext(options)
    {
        public DbSet<TestMultiTenantEntity> MultiTenantEntities => Set<TestMultiTenantEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<TestMultiTenantEntity>(b =>
            {
                b.Property(e => e.Id).ValueGeneratedNever();
                b.HasQueryFilter(GranitFilterNames.MultiTenant, e => e.TenantId == filterTenantId);
            });
    }

    private sealed class FilteredTenantDbContextFactory(Guid? filterTenantId)
        : IDbContextFactory<FilteredTenantDbContext>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        public FilteredTenantDbContext CreateDbContext()
        {
            DbContextOptions<FilteredTenantDbContext> options =
                new DbContextOptionsBuilder<FilteredTenantDbContext>()
                    .UseInMemoryDatabase(_databaseName)
                    .Options;
            return new FilteredTenantDbContext(options, filterTenantId);
        }

        public Task<FilteredTenantDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class FilteredTenantStore(
        IDbContextFactory<FilteredTenantDbContext> contextFactory,
        ICurrentTenant currentTenant,
        IHostAccessContext? hostAccess,
        PersistenceMetrics metrics)
        : EfStoreBase<TestMultiTenantEntity, FilteredTenantDbContext>(contextFactory, currentTenant, hostAccess, metrics)
    {
        public IQueryable<TestMultiTenantEntity> QueryEntities(FilteredTenantDbContext db) => Query(db);
    }
}
