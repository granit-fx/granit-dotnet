using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Documents.Options;
using Granit.Guids;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Postgres-backed tests for <see cref="TenantQuotaService"/>. Pins the atomic SQL
/// write path: 100 parallel increments must accumulate exactly — the read-then-write
/// race that motivates <c>ExecuteUpdateAsync</c> would surface as a lost-update here.
/// </summary>
public sealed class TenantQuotaServicePostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestDbContextFactory _factory = null!;
    private TenantQuotaService _sut = null!;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    public TenantQuotaServicePostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE documents_tenant_storage_quotas RESTART IDENTITY CASCADE;");

        _factory = new TestDbContextFactory(options);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        IOptions<GranitDocumentsOptions> opts = Microsoft.Extensions.Options.Options.Create(new GranitDocumentsOptions
        {
            DefaultTenantQuotaBytes = 5L * 1024L * 1024L * 1024L,
        });

        _sut = new TenantQuotaService(_factory, new SimpleGuidGenerator(), clock, opts);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task IncrementAsync_AccumulatesExactly_Under100ParallelCalls()
    {
        // Bootstrap once so the parallel batch exercises only the atomic UPDATE path
        // (the lazy-create branch handles its own contention via the unique index).
        await _sut.EnsureTenantQuotaAsync(TenantId, TestContext.Current.CancellationToken);

        const int parallelism = 100;
        const long deltaPerCall = 1024;
        var writers = new Task[parallelism];
        for (int i = 0; i < parallelism; i++)
        {
            writers[i] = _sut.IncrementAsync(TenantId, deltaPerCall, TestContext.Current.CancellationToken);
        }
        await Task.WhenAll(writers);

        TenantStorageQuota? quota = await _sut.GetAsync(TenantId, TestContext.Current.CancellationToken);
        quota.ShouldNotBeNull();
        // No lost updates — the atomic SQL UPDATE means each delta lands.
        quota.UsageBytes.ShouldBe(parallelism * deltaPerCall);
    }

    [Fact]
    public async Task TryReserveAsync_RejectsWhenWouldExceedLimit_AcceptsWithinLimit()
    {
        // F7.2 acceptance: tenant at 99% quota uploads 100 MB → rejected;
        // tenant at 50% uploads 100 MB → accepted.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        const long limit = 1_000_000_000; // 1 GB for this test (overrides default)
        const long uploadBytes = 100_000_000; // 100 MB

        // Bootstrap quota row + override LimitBytes via direct EF Core update.
        await _sut.EnsureTenantQuotaAsync(tenantA, TestContext.Current.CancellationToken);
        await _sut.EnsureTenantQuotaAsync(tenantB, TestContext.Current.CancellationToken);
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await db.TenantStorageQuotas
            .Where(q => q.TenantId == tenantA || q.TenantId == tenantB)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.LimitBytes, _ => limit), TestContext.Current.CancellationToken);

        // Tenant A at 99% — IncrementAsync directly (already rounded under limit).
        await _sut.IncrementAsync(tenantA, 990_000_000, TestContext.Current.CancellationToken);
        // Tenant B at 50%.
        await _sut.IncrementAsync(tenantB, 500_000_000, TestContext.Current.CancellationToken);

        bool aReserved = await _sut.TryReserveAsync(tenantA, uploadBytes, TestContext.Current.CancellationToken);
        bool bReserved = await _sut.TryReserveAsync(tenantB, uploadBytes, TestContext.Current.CancellationToken);

        aReserved.ShouldBeFalse();
        bReserved.ShouldBeTrue();

        // Tenant B's usage advanced; tenant A's usage unchanged (no race).
        TenantStorageQuota? a = await _sut.GetAsync(tenantA, TestContext.Current.CancellationToken);
        TenantStorageQuota? b = await _sut.GetAsync(tenantB, TestContext.Current.CancellationToken);
        a.ShouldNotBeNull();
        b.ShouldNotBeNull();
        a.UsageBytes.ShouldBe(990_000_000);
        b.UsageBytes.ShouldBe(600_000_000);
    }

    [Fact]
    public async Task EnsureTenantQuotaAsync_ConcurrentBootstrap_ConvergesOnSingleRow()
    {
        var tenant = Guid.NewGuid();

        const int parallelism = 20;
        var tasks = new Task<TenantStorageQuota>[parallelism];
        for (int i = 0; i < parallelism; i++)
        {
            tasks[i] = _sut.EnsureTenantQuotaAsync(tenant, TestContext.Current.CancellationToken);
        }
        TenantStorageQuota[] results = await Task.WhenAll(tasks);

        // All callers must observe the same row id — the unique index on (TenantId)
        // rejects duplicate inserts and the catch handler re-reads the winner.
        results.Select(r => r.Id).Distinct().Count().ShouldBe(1);
    }

    private sealed class SimpleGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private sealed class TestDbContextFactory(DbContextOptions<DocumentsDbContext> options)
        : IDbContextFactory<DocumentsDbContext>
    {
        public DocumentsDbContext CreateDbContext() => new(options);

        public Task<DocumentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentsDbContext>(new(options));
    }
}
