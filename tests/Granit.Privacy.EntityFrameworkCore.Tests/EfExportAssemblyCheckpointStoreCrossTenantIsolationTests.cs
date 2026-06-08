// EF1001 noise suppressed at the project level — these tests intentionally
// instantiate the internal store via InternalsVisibleTo.
using Granit.Encryption;
using Granit.MultiTenancy;
using Granit.Privacy.DataExport;
using Granit.Privacy.EntityFrameworkCore.DataExport.Internal;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Privacy.EntityFrameworkCore.Tests;

/// <summary>
/// Locks the cross-tenant isolation invariant for <see cref="EfExportAssemblyCheckpointStore"/>.
/// The store deliberately bypasses <c>GranitFilterNames.MultiTenant</c> on every
/// read/write because the export assembly job's target tenant id is dictated by the
/// message payload, which may differ from the ambient <c>ICurrentTenant</c>. Isolation
/// rests entirely on the explicit <c>r.TenantId == tenantId</c> predicate — these tests
/// fail loudly if a future refactor drops it.
/// </summary>
public sealed class EfExportAssemblyCheckpointStoreCrossTenantIsolationTests
{
    private static readonly Guid TenantA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetAsync_DoesNotReturnAnotherTenantsRow_WithSameRequestId()
    {
        // Same request id under two tenants — reading tenant A MUST return tenant
        // A's checkpoint, never tenant B's or the ambient tenant's.
        await using SqliteHarness h = await SqliteHarness.CreateAsync(
            new FakeCurrentTenant { Id = TenantA }, TestContext.Current.CancellationToken);

        var sut = new EfExportAssemblyCheckpointStore(h.Factory, TimeProvider.System);
        var requestId = Guid.NewGuid();
        ExportAssemblyCheckpoint a = new(LastCompletedShardIndex: 1, NextFragmentIndex: 5, CompletedShardObjectKeys: ["a-000.zip", "a-001.zip"]);
        ExportAssemblyCheckpoint b = new(LastCompletedShardIndex: 2, NextFragmentIndex: 9, CompletedShardObjectKeys: ["b-000.zip", "b-001.zip", "b-002.zip"]);

        await sut.SetAsync(requestId, TenantA, a, TestContext.Current.CancellationToken);
        await sut.SetAsync(requestId, TenantB, b, TestContext.Current.CancellationToken);

        ExportAssemblyCheckpoint? readA = await sut.GetAsync(requestId, TenantA, TestContext.Current.CancellationToken);
        ExportAssemblyCheckpoint? readB = await sut.GetAsync(requestId, TenantB, TestContext.Current.CancellationToken);

        readA.ShouldNotBeNull();
        readA.LastCompletedShardIndex.ShouldBe(1);
        readA.CompletedShardObjectKeys.ShouldBe(["a-000.zip", "a-001.zip"]);

        readB.ShouldNotBeNull();
        readB.LastCompletedShardIndex.ShouldBe(2);
        readB.CompletedShardObjectKeys.ShouldBe(["b-000.zip", "b-001.zip", "b-002.zip"]);
    }

    [Fact]
    public async Task ClearAsync_DoesNotDeleteAnotherTenantsRow()
    {
        // ExecuteDelete bypasses change tracking — a missing predicate would wipe
        // every row matching RequestId across all tenants.
        await using SqliteHarness h = await SqliteHarness.CreateAsync(
            new FakeCurrentTenant { Id = TenantA }, TestContext.Current.CancellationToken);

        var sut = new EfExportAssemblyCheckpointStore(h.Factory, TimeProvider.System);
        var requestId = Guid.NewGuid();
        ExportAssemblyCheckpoint a = new(0, 1, ["a-000.zip"]);
        ExportAssemblyCheckpoint b = new(0, 1, ["b-000.zip"]);

        await sut.SetAsync(requestId, TenantA, a, TestContext.Current.CancellationToken);
        await sut.SetAsync(requestId, TenantB, b, TestContext.Current.CancellationToken);

        await sut.ClearAsync(requestId, TenantA, TestContext.Current.CancellationToken);

        (await sut.GetAsync(requestId, TenantA, TestContext.Current.CancellationToken)).ShouldBeNull();
        // Tenant B's row must survive.
        ExportAssemblyCheckpoint? readB = await sut.GetAsync(requestId, TenantB, TestContext.Current.CancellationToken);
        readB.ShouldNotBeNull();
        readB.CompletedShardObjectKeys.ShouldBe(["b-000.zip"]);
    }

    [Fact]
    public async Task SetAsync_ForAmbientTenantA_CanWriteForADifferentTenantB()
    {
        // Background workers run under their own tenant scope (set by the job
        // handler). The store bypasses the ambient tenant filter so a worker
        // dispatched for tenant B can checkpoint against tenant B even if the
        // ambient ICurrentTenant happens to point elsewhere.
        await using SqliteHarness h = await SqliteHarness.CreateAsync(
            new FakeCurrentTenant { Id = TenantA }, TestContext.Current.CancellationToken);

        var sut = new EfExportAssemblyCheckpointStore(h.Factory, TimeProvider.System);
        var requestId = Guid.NewGuid();
        ExportAssemblyCheckpoint cp = new(0, 1, ["b-000.zip"]);

        await sut.SetAsync(requestId, TenantB, cp, TestContext.Current.CancellationToken);

        (await sut.GetAsync(requestId, TenantB, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        (await sut.GetAsync(requestId, TenantA, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_RoundTripsAllCheckpointFields()
    {
        await using SqliteHarness h = await SqliteHarness.CreateAsync(
            new FakeCurrentTenant { Id = TenantA }, TestContext.Current.CancellationToken);

        var sut = new EfExportAssemblyCheckpointStore(h.Factory, TimeProvider.System);
        var requestId = Guid.NewGuid();
        ExportAssemblyCheckpoint cp = new(
            LastCompletedShardIndex: 3,
            NextFragmentIndex: 17,
            CompletedShardObjectKeys: ["personal-data-export/x-000.zip", "personal-data-export/x-001.zip", "personal-data-export/x-002.zip", "personal-data-export/x-003.zip"]);

        await sut.SetAsync(requestId, TenantA, cp, TestContext.Current.CancellationToken);
        ExportAssemblyCheckpoint? read = await sut.GetAsync(requestId, TenantA, TestContext.Current.CancellationToken);

        read.ShouldNotBeNull();
        read.LastCompletedShardIndex.ShouldBe(3);
        read.NextFragmentIndex.ShouldBe(17);
        read.CompletedShardObjectKeys.Count.ShouldBe(4);
        read.CompletedShardObjectKeys[^1].ShouldBe("personal-data-export/x-003.zip");
    }

    [Fact]
    public async Task SetAsync_TwiceUpdatesInPlace_DoesNotInsertDuplicate()
    {
        await using SqliteHarness h = await SqliteHarness.CreateAsync(
            new FakeCurrentTenant { Id = TenantA }, TestContext.Current.CancellationToken);

        var sut = new EfExportAssemblyCheckpointStore(h.Factory, TimeProvider.System);
        var requestId = Guid.NewGuid();

        await sut.SetAsync(requestId, TenantA, new ExportAssemblyCheckpoint(0, 1, ["a-000.zip"]), TestContext.Current.CancellationToken);
        await sut.SetAsync(requestId, TenantA, new ExportAssemblyCheckpoint(1, 4, ["a-000.zip", "a-001.zip"]), TestContext.Current.CancellationToken);

        await using PrivacyDbContext db = await h.Factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        int rowCount = await db.Set<ExportAssemblyCheckpointRow>()
            .IgnoreQueryFilters([Granit.Persistence.EntityFrameworkCore.GranitFilterNames.MultiTenant])
            .CountAsync(r => r.RequestId == requestId && r.TenantId == TenantA, TestContext.Current.CancellationToken);

        rowCount.ShouldBe(1);

        ExportAssemblyCheckpoint? read = await sut.GetAsync(requestId, TenantA, TestContext.Current.CancellationToken);
        read.ShouldNotBeNull();
        read.LastCompletedShardIndex.ShouldBe(1);
    }

    /// <summary>
    /// In-memory SQLite harness — gives us real SQL translation (the InMemory provider
    /// lies about query filters) without a Postgres container.
    /// </summary>
    private sealed class SqliteHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public IDbContextFactory<PrivacyDbContext> Factory { get; }

        private SqliteHarness(SqliteConnection connection, IDbContextFactory<PrivacyDbContext> factory)
        {
            _connection = connection;
            Factory = factory;
        }

        public static async Task<SqliteHarness> CreateAsync(FakeCurrentTenant tenant, CancellationToken ct)
        {
            SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync(ct);

            DbContextOptions<PrivacyDbContext> options = new DbContextOptionsBuilder<PrivacyDbContext>()
                .UseSqlite(connection)
                .Options;

            IStringEncryptionService encryption = new PassthroughEncryption();
            var factory = new FixedFactory(options, encryption, tenant);

            await using (PrivacyDbContext db = await factory.CreateDbContextAsync(ct))
            {
                await db.Database.EnsureCreatedAsync(ct);
            }

            return new SqliteHarness(connection, factory);
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

        private sealed class FixedFactory(
            DbContextOptions<PrivacyDbContext> options,
            IStringEncryptionService encryption,
            ICurrentTenant tenant) : IDbContextFactory<PrivacyDbContext>
        {
            public PrivacyDbContext CreateDbContext() => new(options, encryption, tenant);
            public Task<PrivacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new PrivacyDbContext(options, encryption, tenant));
        }

        private sealed class PassthroughEncryption : IStringEncryptionService
        {
            public string Encrypt(string plainText) => plainText;
            public string? Decrypt(string cipherText) => cipherText;
        }
    }

}
