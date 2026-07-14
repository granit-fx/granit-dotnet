// EF1001 noise suppressed at the project level — these tests intentionally
// instantiate the internal stores and DbContext via InternalsVisibleTo.
using Granit.DataExchange.EntityFrameworkCore.Internal;
using Granit.DataExchange.EntityFrameworkCore.Internal.Retention;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Reporting;
using Granit.Domain.ValueObjects;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Retention;

/// <summary>
/// Locks the cross-tenant SWEEP invariant for <see cref="EfDataExchangeRetentionStore"/> (and its
/// import/export sides). The daily retention sweep runs with no ambient tenant, so every query
/// must see every tenant partition — including the host partition (<c>TenantId == null</c>). If a
/// future refactor drops the <c>QueryAcrossTenants</c> bypass and falls back to the implicit
/// tenant filter, the sweep silently collapses to the host partition and these tests fail loudly —
/// the exact GDPR Art. 5(1)(e) storage-limitation gap this guard exists to catch. Pattern-copied
/// from <c>EfDeletionRequestTrackerCrossTenantSweepTests</c> (Granit.Privacy.EntityFrameworkCore.Tests).
/// </summary>
public sealed class EfDataExchangeRetentionStoreCrossTenantSweepTests
{
    private static readonly Guid TenantA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task GetImportJobsWithExpiredFilesAsync_ReturnsRowsFromEveryPartition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqliteHarness h = await SqliteHarness.CreateAsync(ct);

        ImportJob tenantAJob = NewCompletedImportJob();
        ImportJob tenantBJob = NewCompletedImportJob();
        ImportJob hostJob = NewCompletedImportJob();
        await h.SeedImportAsync(TenantA, tenantAJob, ct);
        await h.SeedImportAsync(TenantB, tenantBJob, ct);
        await h.SeedImportAsync(null, hostJob, ct);

        EfImportJobRetentionStore sut = new(h.Factory, new FakeCurrentTenant());

        IReadOnlyList<ImportJob> expired = await sut.GetJobsWithExpiredFilesAsync(Now.AddDays(1), 100, ct);

        expired.Select(j => j.Id).ShouldBe([tenantAJob.Id, tenantBJob.Id, hostJob.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task GetExportJobsWithExpiredFilesAsync_ReturnsRowsFromEveryPartition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqliteHarness h = await SqliteHarness.CreateAsync(ct);

        ExportJob tenantAJob = NewCompletedExportJob();
        ExportJob tenantBJob = NewCompletedExportJob();
        ExportJob hostJob = NewCompletedExportJob();
        await h.SeedExportAsync(TenantA, tenantAJob, ct);
        await h.SeedExportAsync(TenantB, tenantBJob, ct);
        await h.SeedExportAsync(null, hostJob, ct);

        EfExportJobRetentionStore sut = new(h.Factory, new FakeCurrentTenant());

        IReadOnlyList<ExportJob> expired = await sut.GetJobsWithExpiredFilesAsync(Now.AddDays(1), 100, ct);

        expired.Select(j => j.Id).ShouldBe([tenantAJob.Id, tenantBJob.Id, hostJob.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task GetExpiredTerminalImportJobsAsync_ReturnsRowsFromEveryPartition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqliteHarness h = await SqliteHarness.CreateAsync(ct);

        ImportJob tenantAJob = NewCompletedImportJob();
        ImportJob tenantBJob = NewCompletedImportJob();
        ImportJob hostJob = NewCompletedImportJob();
        await h.SeedImportAsync(TenantA, tenantAJob, ct);
        await h.SeedImportAsync(TenantB, tenantBJob, ct);
        await h.SeedImportAsync(null, hostJob, ct);

        EfImportJobRetentionStore sut = new(h.Factory, new FakeCurrentTenant());

        IReadOnlyList<ImportJob> expired = await sut.GetExpiredTerminalJobsAsync(Now.AddDays(1), 100, ct);

        expired.Select(j => j.Id).ShouldBe([tenantAJob.Id, tenantBJob.Id, hostJob.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task GetExpiredTerminalExportJobsAsync_ReturnsRowsFromEveryPartition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqliteHarness h = await SqliteHarness.CreateAsync(ct);

        ExportJob tenantAJob = NewCompletedExportJob();
        ExportJob tenantBJob = NewCompletedExportJob();
        ExportJob hostJob = NewCompletedExportJob();
        await h.SeedExportAsync(TenantA, tenantAJob, ct);
        await h.SeedExportAsync(TenantB, tenantBJob, ct);
        await h.SeedExportAsync(null, hostJob, ct);

        EfExportJobRetentionStore sut = new(h.Factory, new FakeCurrentTenant());

        IReadOnlyList<ExportJob> expired = await sut.GetExpiredTerminalJobsAsync(Now.AddDays(1), 100, ct);

        expired.Select(j => j.Id).ShouldBe([tenantAJob.Id, tenantBJob.Id, hostJob.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task GetStuckImportJobsAsync_ReturnsRowsFromEveryPartition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqliteHarness h = await SqliteHarness.CreateAsync(ct);

        ImportJob tenantAJob = NewExecutingImportJob();
        ImportJob tenantBJob = NewExecutingImportJob();
        ImportJob hostJob = NewExecutingImportJob();
        await h.SeedImportAsync(TenantA, tenantAJob, ct);
        await h.SeedImportAsync(TenantB, tenantBJob, ct);
        await h.SeedImportAsync(null, hostJob, ct);

        EfImportJobRetentionStore sut = new(h.Factory, new FakeCurrentTenant());

        IReadOnlyList<ImportJob> stuck = await sut.GetStuckJobsAsync(Now.AddDays(1), 100, ct);

        stuck.Select(j => j.Id).ShouldBe([tenantAJob.Id, tenantBJob.Id, hostJob.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task GetStuckExportJobsAsync_ReturnsRowsFromEveryPartition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqliteHarness h = await SqliteHarness.CreateAsync(ct);

        ExportJob tenantAJob = NewExportingJob();
        ExportJob tenantBJob = NewExportingJob();
        ExportJob hostJob = NewExportingJob();
        await h.SeedExportAsync(TenantA, tenantAJob, ct);
        await h.SeedExportAsync(TenantB, tenantBJob, ct);
        await h.SeedExportAsync(null, hostJob, ct);

        EfExportJobRetentionStore sut = new(h.Factory, new FakeCurrentTenant());

        IReadOnlyList<ExportJob> stuck = await sut.GetStuckJobsAsync(Now.AddDays(1), 100, ct);

        stuck.Select(j => j.Id).ShouldBe([tenantAJob.Id, tenantBJob.Id, hostJob.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task GetImportJobsWithExpiredFilesAsync_ExcludesFilesAlreadyMarkedDeleted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqliteHarness h = await SqliteHarness.CreateAsync(ct);

        ImportJob alreadyPurged = NewCompletedImportJob();
        alreadyPurged.MarkFileDeleted(Now.AddDays(-1));
        ImportJob stillPending = NewCompletedImportJob();
        await h.SeedImportAsync(TenantA, alreadyPurged, ct);
        await h.SeedImportAsync(TenantA, stillPending, ct);

        EfImportJobRetentionStore sut = new(h.Factory, new FakeCurrentTenant());

        IReadOnlyList<ImportJob> expired = await sut.GetJobsWithExpiredFilesAsync(Now.AddDays(1), 100, ct);

        expired.Select(j => j.Id).ShouldBe([stillPending.Id]);
    }

    // ── Fixtures ──────────────────────────────────────────────────────────

    private static ImportJob NewExecutingImportJob()
    {
        var job = ImportJob.Create(
            Guid.NewGuid(), "Test.Import", "TestEntity", "data.csv", "text/csv", 1024,
            BlobReference.Create("import/blob-" + Guid.NewGuid()));
        job.MarkAsPreviewed();
        job.ConfirmMappings([]);
        job.MarkAsExecuting();
        job.CreatedAt = Now.AddDays(-10);
        return job;
    }

    private static ImportJob NewCompletedImportJob()
    {
        ImportJob job = NewExecutingImportJob();
        job.Complete(
            ImportJobStatus.Completed,
            new ImportReport
            {
                TotalRows = 1,
                SucceededRows = 1,
                FailedRows = 0,
                SkippedRows = 0,
                InsertedRows = 1,
                UpdatedRows = 0,
                Duration = TimeSpan.Zero,
                FinalStatus = ImportJobStatus.Completed,
                RowErrors = [],
            },
            Now.AddDays(-5));
        return job;
    }

    private static ExportJob NewExportingJob()
    {
        var job = ExportJob.Create(
            Guid.NewGuid(), "Test.Export", "csv",
            new ExportRequest("Test.Export", "csv", null, false, null, null, null, null));
        job.MarkAsExporting();
        job.CreatedAt = Now.AddDays(-10);
        return job;
    }

    private static ExportJob NewCompletedExportJob()
    {
        ExportJob job = NewExportingJob();
        job.Complete(BlobReference.Create("export/blob-" + Guid.NewGuid()), "export.csv", 1, Now.AddDays(-5));
        return job;
    }

    /// <summary>
    /// In-memory SQLite harness — real SQL translation (the InMemory provider lies about query
    /// filters) without a Postgres container. Seeds rows under an explicit tenant, then runs the
    /// SUT with no ambient tenant. All contexts share the single open connection.
    /// </summary>
    private sealed class SqliteHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<DataExchangeDbContext> _options;

        public IDbContextFactory<DataExchangeDbContext> Factory { get; }

        private SqliteHarness(SqliteConnection connection, DbContextOptions<DataExchangeDbContext> options)
        {
            _connection = connection;
            _options = options;
            // Factory models the background job: no ambient tenant.
            Factory = new FixedFactory(options);
        }

        public static async Task<SqliteHarness> CreateAsync(CancellationToken ct)
        {
            SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync(ct);

            DbContextOptions<DataExchangeDbContext> options = new DbContextOptionsBuilder<DataExchangeDbContext>()
                .UseSqlite(connection)
                .ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>()
                .Options;

            var harness = new SqliteHarness(connection, options);
            await using DataExchangeDbContext db = new(options, new FakeCurrentTenant());
            await db.Database.EnsureCreatedAsync(ct);

            return harness;
        }

        public async Task SeedImportAsync(Guid? tenantId, ImportJob job, CancellationToken ct)
        {
            // TenantId is stamped by an interceptor wired at DI registration in production; the raw
            // test context has none, so set it explicitly (same approach as the checkpoint store).
            ((Granit.Domain.IMultiTenant)job).TenantId = tenantId;
            await using DataExchangeDbContext db = new(_options, new FakeCurrentTenant());
            db.Set<ImportJob>().Add(job);
            await db.SaveChangesAsync(ct);
        }

        public async Task SeedExportAsync(Guid? tenantId, ExportJob job, CancellationToken ct)
        {
            ((Granit.Domain.IMultiTenant)job).TenantId = tenantId;
            await using DataExchangeDbContext db = new(_options, new FakeCurrentTenant());
            db.Set<ExportJob>().Add(job);
            await db.SaveChangesAsync(ct);
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

        private sealed class FixedFactory(DbContextOptions<DataExchangeDbContext> options)
            : IDbContextFactory<DataExchangeDbContext>
        {
            public DataExchangeDbContext CreateDbContext() => new(options, new FakeCurrentTenant());
            public Task<DataExchangeDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new DataExchangeDbContext(options, new FakeCurrentTenant()));
        }
    }

    /// <summary>
    /// Remaps <see cref="DateTimeOffset"/> columns to SQLite-compatible integers so cutoff
    /// comparisons (<c>CompletedAt &lt; cutoff</c>, etc.) translate. Same shape as the companion
    /// customizer in <c>EfDeletionRequestTrackerCrossTenantSweepTests</c>.
    /// </summary>
    private sealed class SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
        : RelationalModelCustomizer(dependencies)
    {
        private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetConverter = new(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter = new(
            v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
            v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (IMutableProperty property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(DateTimeOffsetConverter);
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(NullableDateTimeOffsetConverter);
                    }
                }
            }
        }
    }
}
