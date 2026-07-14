using Granit.DataExchange.EntityFrameworkCore.Internal;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Execution;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Execution;
using Granit.DataExchange.Import.Identity;
using Granit.DataExchange.Import.Reporting;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Execution;

/// <summary>
/// SQLite-backed (real transactions + savepoints, unlike the InMemory provider) tests for
/// <see cref="EfImportExecutor{TEntity, TContext}"/> — in particular the update path, which used
/// to call <c>SetValues</c> on an entity tracked by a resolver's own, already-disposed context and
/// silently persist zero rows. Every update test re-reads the database through a fresh context.
/// </summary>
public sealed class EfImportExecutorTests : IDisposable
{
    private readonly SqliteConnection _appConnection;
    private readonly SqliteConnection _dataExchangeConnection;
    private readonly DbContextOptions<TestAppDbContext> _appOptions;
    private readonly DbContextOptions<DataExchangeDbContext> _dataExchangeOptions;
    private readonly ICurrentTenant _tenant = GranitDesignTime.CurrentTenant;
    private readonly FakeClock _clock = new();
    private readonly FakeGuidGenerator _guidGenerator = new();

    public EfImportExecutorTests()
    {
        _appConnection = new SqliteConnection("DataSource=:memory:");
        _appConnection.Open();
        _appOptions = new DbContextOptionsBuilder<TestAppDbContext>().UseSqlite(_appConnection).Options;
        using (TestAppDbContext schema = new(_appOptions))
        {
            schema.Database.EnsureCreated();
        }

        _dataExchangeConnection = new SqliteConnection("DataSource=:memory:");
        _dataExchangeConnection.Open();
        _dataExchangeOptions = new DbContextOptionsBuilder<DataExchangeDbContext>()
            .UseSqlite(_dataExchangeConnection)
            .AddInterceptors(new ConcurrencyStampInterceptor())
            .Options;
        using (DataExchangeDbContext schema = new(_dataExchangeOptions, _tenant))
        {
            schema.Database.EnsureCreated();
        }
    }

    public void Dispose()
    {
        _appConnection.Dispose();
        _dataExchangeConnection.Dispose();
    }

    private TestAppDbContext CreateAppContext() => new(_appOptions);

    private DataExchangeDbContext CreateDataExchangeContext() => new(_dataExchangeOptions, _tenant);

    private EfImportExecutor<TestEntity, TestAppDbContext> CreateExecutor(ImportDefinition<TestEntity>? definition = null) =>
        new(
            new AppContextFactory(_appOptions),
            new DataExchangeContextFactory(_dataExchangeOptions, _tenant),
            definition ?? new TestImportDefinition(),
            _tenant,
            _clock,
            _guidGenerator,
            NullLogger<EfImportExecutor<TestEntity, TestAppDbContext>>.Instance);

    private static async IAsyncEnumerable<RowOutcome<TestEntity>> ToStream(IEnumerable<RowOutcome<TestEntity>> rows)
    {
        foreach (RowOutcome<TestEntity> row in rows)
        {
            yield return row;
        }

        await Task.CompletedTask;
    }

    private async Task SeedAsync(TestEntity entity)
    {
        await using TestAppDbContext context = CreateAppContext();
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_inserts_all_entities()
    {
        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        ImportExecutionOptions options = new() { BatchSize = 100 };

        List<RowOutcome<TestEntity>> rows =
        [
            RowOutcome<TestEntity>.Ok(1, new TestEntity { Name = "A", Niss = "1" }),
            RowOutcome<TestEntity>.Ok(2, new TestEntity { Name = "B", Niss = "2" }),
        ];

        ImportReport report = await executor.ExecuteAsync(
            ToStream(rows), options, null, TestContext.Current.CancellationToken);

        report.TotalRows.ShouldBe(2);
        report.SucceededRows.ShouldBe(2);
        report.InsertedRows.ShouldBe(2);
        report.UpdatedRows.ShouldBe(0);
        report.FailedRows.ShouldBe(0);
        report.FinalStatus.ShouldBe(ImportJobStatus.Completed);

        await using TestAppDbContext verify = CreateAppContext();
        (await verify.TestEntities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_default_insert_identity_null_stamps_insert()
    {
        // RowOutcome.Ok with no Identity means "insert-only" per its own doc comment.
        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        List<RowOutcome<TestEntity>> rows = [RowOutcome<TestEntity>.Ok(1, new TestEntity { Name = "A" })];

        ImportReport report = await executor.ExecuteAsync(
            ToStream(rows), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken);

        report.InsertedRows.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_update_path_actually_persists_changed_values()
    {
        // The regression this rewrite fixes: the old executor called SetValues on an entity
        // tracked by the RESOLVER's own (disposed) context, which silently no-oped the UPDATE.
        var existingId = Guid.NewGuid();
        await SeedAsync(new TestEntity { Id = existingId, Name = "Original", Niss = "123456", Age = 30 });

        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        TestEntity incoming = new() { Name = "Updated", Niss = "123456", Age = 99 };
        List<RowOutcome<TestEntity>> rows =
        [
            RowOutcome<TestEntity>.Ok(1, incoming, RecordIdentity.Upsert(new EntityKey("123456"), EntityKeyKind.BusinessKey)),
        ];

        ImportReport report = await executor.ExecuteAsync(
            ToStream(rows), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken);

        report.UpdatedRows.ShouldBe(1);
        report.InsertedRows.ShouldBe(0);
        report.FinalStatus.ShouldBe(ImportJobStatus.Completed);

        // Re-read through a brand-new context — proves the value actually changed in the database,
        // not just in a detached in-memory instance.
        await using TestAppDbContext verify = CreateAppContext();
        TestEntity persisted = await verify.TestEntities.SingleAsync(TestContext.Current.CancellationToken);
        persisted.Id.ShouldBe(existingId);
        persisted.Name.ShouldBe("Updated");

        // Age is declared ExcludeOnUpdate on TestImportDefinition — must NOT be overwritten.
        persisted.Age.ShouldBe(30);
    }

    [Fact]
    public async Task ExecuteAsync_strict_update_with_no_match_fails_with_missing_target()
    {
        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        TestEntity incoming = new() { Name = "Ghost", Niss = "does-not-exist" };
        List<RowOutcome<TestEntity>> rows =
        [
            RowOutcome<TestEntity>.Ok(1, incoming, RecordIdentity.Update(new EntityKey("does-not-exist"), EntityKeyKind.BusinessKey)),
        ];

        ImportReport report = await executor.ExecuteAsync(
            ToStream(rows), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken);

        report.FailedRows.ShouldBe(1);
        report.InsertedRows.ShouldBe(0);
        report.UpdatedRows.ShouldBe(0);
        report.RowErrors.ShouldHaveSingleItem().ErrorCodes.ShouldContain(IdentityReasonCodes.MissingTarget);
    }

    [Fact]
    public async Task ExecuteAsync_upsert_with_no_match_inserts()
    {
        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        TestEntity incoming = new() { Name = "New", Niss = "999" };
        List<RowOutcome<TestEntity>> rows =
        [
            RowOutcome<TestEntity>.Ok(1, incoming, RecordIdentity.Upsert(new EntityKey("999"), EntityKeyKind.BusinessKey)),
        ];

        ImportReport report = await executor.ExecuteAsync(
            ToStream(rows), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken);

        report.InsertedRows.ShouldBe(1);
        report.UpdatedRows.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_mixed_insert_update_skip_ambiguous_counts_correctly()
    {
        var existingId = Guid.NewGuid();
        await SeedAsync(new TestEntity { Id = existingId, Name = "Original", Niss = "111" });

        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        List<RowOutcome<TestEntity>> rows =
        [
            RowOutcome<TestEntity>.Ok(1, new TestEntity { Name = "Insert", Niss = "222" },
                RecordIdentity.Upsert(new EntityKey("222"), EntityKeyKind.BusinessKey)),
            RowOutcome<TestEntity>.Ok(2, new TestEntity { Name = "Updated", Niss = "111" },
                RecordIdentity.Upsert(new EntityKey("111"), EntityKeyKind.BusinessKey)),
            RowOutcome<TestEntity>.Ok(3, new TestEntity { Name = "Skip", Niss = "333" },
                RecordIdentity.Skip(IdentityReasonCodes.DuplicateKeyInFile)),
            RowOutcome<TestEntity>.Ok(4, new TestEntity { Name = "Ambiguous" },
                RecordIdentity.Ambiguous(IdentityReasonCodes.MissingKeyComponent)),
            RowOutcome<TestEntity>.Skipped(5),
            RowOutcome<TestEntity>.Failed(6, new ImportRowError(6, ImportRowErrorKind.Conversion, ["x"], "bad row")),
        ];

        ImportReport report = await executor.ExecuteAsync(
            ToStream(rows), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken);

        report.TotalRows.ShouldBe(6);
        report.InsertedRows.ShouldBe(1);
        report.UpdatedRows.ShouldBe(1);
        report.SkippedRows.ShouldBe(2); // identity-Skip + parser-Skipped
        report.FailedRows.ShouldBe(2); // Ambiguous + Failed
        report.SucceededRows.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_poison_row_in_batch_isolates_and_others_persist()
    {
        // A duplicate Niss violates the unique index declared on TestAppDbContext, which is only
        // triggered at SaveChanges time — the batch save fails, then the row-by-row replay
        // isolates exactly the poison row while the rest of the batch still persists.
        await SeedAsync(new TestEntity { Name = "Existing", Niss = "DUP" });

        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        List<RowOutcome<TestEntity>> rows =
        [
            RowOutcome<TestEntity>.Ok(1, new TestEntity { Name = "Good1", Niss = "OK-1" }),
            RowOutcome<TestEntity>.Ok(2, new TestEntity { Name = "Poison", Niss = "DUP" }),
            RowOutcome<TestEntity>.Ok(3, new TestEntity { Name = "Good2", Niss = "OK-2" }),
        ];

        ImportReport report = await executor.ExecuteAsync(
            ToStream(rows), new ImportExecutionOptions { BatchSize = 10 }, null, TestContext.Current.CancellationToken);

        report.FailedRows.ShouldBe(1);
        report.RowErrors.ShouldHaveSingleItem().Kind.ShouldBe(ImportRowErrorKind.Persistence);
        report.RowErrors[0].RowNumber.ShouldBe(2);
        report.InsertedRows.ShouldBe(2);
        report.FinalStatus.ShouldBe(ImportJobStatus.PartiallyCompleted);

        await using TestAppDbContext verify = CreateAppContext();
        List<string> names = await verify.TestEntities.Select(e => e.Name).OrderBy(n => n).ToListAsync(TestContext.Current.CancellationToken);
        names.ShouldBe(["Existing", "Good1", "Good2"]);
    }

    [Fact]
    public async Task ExecuteAsync_fail_fast_stops_and_rethrows()
    {
        await SeedAsync(new TestEntity { Name = "Existing", Niss = "DUP" });

        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        List<RowOutcome<TestEntity>> rows =
        [
            RowOutcome<TestEntity>.Ok(1, new TestEntity { Name = "Good1", Niss = "OK-1" }),
            RowOutcome<TestEntity>.Ok(2, new TestEntity { Name = "Poison", Niss = "DUP" }),
        ];

        ImportExecutionOptions options = new() { BatchSize = 10, ErrorBehavior = ImportErrorBehavior.FailFast };

        await Should.ThrowAsync<DbUpdateException>(() =>
            executor.ExecuteAsync(ToStream(rows), options, null, TestContext.Current.CancellationToken));

        // Everything rolled back — nothing from this failed batch persisted.
        await using TestAppDbContext verify = CreateAppContext();
        (await verify.TestEntities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_operation_canceled_propagates()
    {
        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();

        static async IAsyncEnumerable<RowOutcome<TestEntity>> CancelingStream()
        {
            yield return RowOutcome<TestEntity>.Ok(1, new TestEntity { Name = "A" });
            await Task.CompletedTask;
            throw new OperationCanceledException();
        }

        await Should.ThrowAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(CancelingStream(), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_unexpected_exception_rethrown_and_rolls_back()
    {
        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();

        static async IAsyncEnumerable<RowOutcome<TestEntity>> ThrowingStream()
        {
            yield return RowOutcome<TestEntity>.Ok(1, new TestEntity { Name = "A" });
            await Task.CompletedTask;
            throw new InvalidOperationException("boom");
        }

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(ThrowingStream(), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken));
        ex.Message.ShouldBe("boom");

        await using TestAppDbContext verify = CreateAppContext();
        (await verify.TestEntities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_dry_run_persists_nothing()
    {
        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        List<RowOutcome<TestEntity>> rows =
        [
            RowOutcome<TestEntity>.Ok(1, new TestEntity { Name = "A", Niss = "1" }),
            RowOutcome<TestEntity>.Ok(2, new TestEntity { Name = "B", Niss = "2" }),
        ];

        ImportReport report = await executor.ExecuteAsync(
            ToStream(rows), new ImportExecutionOptions { DryRun = true }, null, TestContext.Current.CancellationToken);

        report.InsertedRows.ShouldBe(2);

        await using TestAppDbContext verify = CreateAppContext();
        (await verify.TestEntities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_empty_stream_returns_completed()
    {
        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();

        ImportReport report = await executor.ExecuteAsync(
            ToStream([]), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken);

        report.TotalRows.ShouldBe(0);
        report.FinalStatus.ShouldBe(ImportJobStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_reports_progress()
    {
        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        List<ImportProgress> progressReports = [];
        SynchronousProgress progress = new(progressReports);
        List<RowOutcome<TestEntity>> rows =
        [
            RowOutcome<TestEntity>.Ok(1, new TestEntity { Name = "A", Niss = "1" }),
            RowOutcome<TestEntity>.Ok(2, new TestEntity { Name = "B", Niss = "2" }),
            RowOutcome<TestEntity>.Ok(3, new TestEntity { Name = "C", Niss = "3" }),
        ];

        await executor.ExecuteAsync(
            ToStream(rows), new ImportExecutionOptions { BatchSize = 2 }, progress, TestContext.Current.CancellationToken);

        progressReports.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_report_duration_is_positive()
    {
        EfImportExecutor<TestEntity, TestAppDbContext> executor = CreateExecutor();
        List<RowOutcome<TestEntity>> rows = [RowOutcome<TestEntity>.Ok(1, new TestEntity { Name = "A" })];

        ImportReport report = await executor.ExecuteAsync(
            ToStream(rows), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken);

        report.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_external_id_insert_writes_mapping_and_reimport_updates()
    {
        TestExternalIdImportDefinition definition = new();
        EfImportExecutor<TestEntity, TestAppDbContext> firstRunExecutor = CreateExecutor(definition);

        TestEntity firstEntity = new() { Name = "Alice", ExternalId = "EXT-1" };
        List<RowOutcome<TestEntity>> firstRows =
        [
            RowOutcome<TestEntity>.Ok(1, firstEntity, RecordIdentity.Insert("EXT-1")),
        ];

        ImportReport firstReport = await firstRunExecutor.ExecuteAsync(
            ToStream(firstRows), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken);

        firstReport.InsertedRows.ShouldBe(1);

        // The mapping row was written after commit.
        await using (DataExchangeDbContext verifyMapping = CreateDataExchangeContext())
        {
            ExternalIdMappingEntity mapping = await verifyMapping.ExternalIdMappings.SingleAsync(TestContext.Current.CancellationToken);
            mapping.ExternalId.ShouldBe("EXT-1");
            mapping.DefinitionName.ShouldBe(definition.Name);
        }

        Guid internalId;
        await using (TestAppDbContext verifyEntity = CreateAppContext())
        {
            internalId = (await verifyEntity.TestEntities.SingleAsync(TestContext.Current.CancellationToken)).Id;
        }

        // Re-import the same external id: the resolver (queried live in a real flow) would now
        // resolve to Update — simulate that directly against the executor.
        EfImportExecutor<TestEntity, TestAppDbContext> secondRunExecutor = CreateExecutor(definition);
        TestEntity secondEntity = new() { Name = "Alice Updated", ExternalId = "EXT-1" };
        List<RowOutcome<TestEntity>> secondRows =
        [
            RowOutcome<TestEntity>.Ok(1, secondEntity, RecordIdentity.Update(new EntityKey(internalId), EntityKeyKind.PrimaryKey)),
        ];

        ImportReport secondReport = await secondRunExecutor.ExecuteAsync(
            ToStream(secondRows), new ImportExecutionOptions(), null, TestContext.Current.CancellationToken);

        secondReport.UpdatedRows.ShouldBe(1);
        secondReport.InsertedRows.ShouldBe(0);

        await using TestAppDbContext verifyNoDuplicate = CreateAppContext();
        List<TestEntity> all = await verifyNoDuplicate.TestEntities.ToListAsync(TestContext.Current.CancellationToken);
        all.ShouldHaveSingleItem();
        all[0].Id.ShouldBe(internalId);
        all[0].Name.ShouldBe("Alice Updated");
    }

    /// <summary>
    /// Synchronous IProgress implementation to avoid <see cref="Progress{T}"/>
    /// posting to the thread pool (which causes race conditions in tests).
    /// </summary>
    private sealed class SynchronousProgress(List<ImportProgress> reports) : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value) => reports.Add(value);
    }

    private sealed class AppContextFactory(DbContextOptions<TestAppDbContext> options) : IDbContextFactory<TestAppDbContext>
    {
        public TestAppDbContext CreateDbContext() => new(options);

        public Task<TestAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class DataExchangeContextFactory(DbContextOptions<DataExchangeDbContext> options, ICurrentTenant tenant)
        : IDbContextFactory<DataExchangeDbContext>
    {
        public DataExchangeDbContext CreateDbContext() => new(options, tenant);

        public Task<DataExchangeDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
