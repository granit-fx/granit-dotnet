using System.Text;
using Granit.DataExchange.Csv.Extensions;
using Granit.DataExchange.EntityFrameworkCore.Extensions;
using Granit.DataExchange.EntityFrameworkCore.Internal;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Stores;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Extensions;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Granit.Domain.ValueObjects;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Granit.Testing.EntityFrameworkCore;
using Granit.Testing.Fakes;
using Granit.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests;

/// <summary>
/// End-to-end keel for the typed import pipeline on real SQLite:
/// upload → preview → confirm mappings → execute, then assert the report,
/// the persisted job state, and the actually-inserted application entities.
/// </summary>
public sealed class ImportEndToEndTests : IDisposable
{
    private const string DefinitionName = "Test.TestEntityImport";

    private const string Csv =
        "Name,Email,NISS,Age\n" +
        "Alice,alice@test.com,111,30\n" +
        "Bob,bob@test.com,222,40\n" +
        "Carol,carol@test.com,333,50\n" +
        "Dave,dave@test.com,444,notanumber\n" +
        ",,,\n";

    private readonly SqliteDbContextFactory<TestAppDbContext> _appSqlite = new();
    private readonly SqliteDataExchangeContextFactory _dataExchangeFactory = new();
    private readonly SqliteAppContextFactory _appFactory;
    private readonly ServiceProvider _provider;

    public ImportEndToEndTests()
    {
        // Create the app schema once; subsequent contexts reuse the shared connection.
        _appSqlite.CreateContext().Dispose();
        _appFactory = new SqliteAppContextFactory(_appSqlite);

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton<IClock>(new FakeClock());
        services.AddSingleton<IGuidGenerator>(new FakeGuidGenerator());
        services.AddSingleton<ICurrentTenant>(GranitDesignTime.CurrentTenant);
        services.AddSingleton<IDbContextFactory<TestAppDbContext>>(_appFactory);
        services.AddSingleton<IDbContextFactory<DataExchangeDbContext>>(_dataExchangeFactory);

        // Real EF job store + test-local in-memory file provider, registered before
        // AddGranitDataImport so the TryAdd null-object defaults do not win.
        services.AddSingleton<IDataExchangeFileProvider>(new InMemoryFileProvider());
        services.AddScoped<EfImportJobStore>();
        services.AddScoped<IImportJobReader>(sp => sp.GetRequiredService<EfImportJobStore>());
        services.AddScoped<IImportJobWriter>(sp => sp.GetRequiredService<EfImportJobStore>());

        services.AddGranitDataImport();
        services.AddGranitDataExchangeCsv();
        services.AddImportDefinition<TestEntity, TestImportDefinition>();
        services.AddImportExecutor<TestEntity, TestAppDbContext>();
        services.AddBusinessKeyResolver<TestEntity, TestAppDbContext>();

        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _dataExchangeFactory.Dispose();
        _appSqlite.Dispose();
    }

    [Fact]
    public async Task Full_import_flow_inserts_valid_rows_and_reports_broken_ones()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = _provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        // 1. Upload
        Guid jobId = await UploadAsync(sp, cancellationToken);

        // 2. Preview — headers, suggestions, and the Previewed transition
        IImportPreviewService previewService = sp.GetRequiredService<IImportPreviewService>();
        ImportPreviewResult? preview = await previewService.PreviewAsync(jobId, cancellationToken);
        preview.ShouldNotBeNull();
        preview!.Headers.ShouldBe(["Name", "Email", "NISS", "Age"]);
        preview.Suggestions.Count.ShouldBe(4);
        preview.Suggestions.Select(s => s.TargetProperty)
            .ShouldBe(["Name", "Email", "Niss", "Age"], ignoreOrder: true);

        // 3. Confirm mappings — same domain path the endpoint drives
        IImportJobReader jobReader = sp.GetRequiredService<IImportJobReader>();
        IImportJobWriter jobWriter = sp.GetRequiredService<IImportJobWriter>();
        ImportJob job = (await jobReader.GetAsync(jobId, cancellationToken))!;
        job.Status.ShouldBe(ImportJobStatus.Previewed);
        job.ConfirmMappings(preview.Suggestions);
        await jobWriter.UpdateAsync(job, job.ConcurrencyStamp, cancellationToken);

        // 4. Execute
        IImportOrchestrator orchestrator = sp.GetRequiredService<IImportOrchestrator>();
        ImportReport report = await orchestrator.ExecuteAsync(jobId, cancellationToken);

        // 5. Report: 3 valid, 1 conversion-broken, 1 all-empty
        report.TotalRows.ShouldBe(5);
        report.SucceededRows.ShouldBe(3);
        report.FailedRows.ShouldBe(1);
        report.SkippedRows.ShouldBe(1);
        report.InsertedRows.ShouldBe(3);
        report.UpdatedRows.ShouldBe(0);
        report.FinalStatus.ShouldBe(ImportJobStatus.PartiallyCompleted);

        ImportRowError error = report.RowErrors.ShouldHaveSingleItem();
        error.Kind.ShouldBe(ImportRowErrorKind.Conversion);
        error.RowNumber.ShouldBe(4);
        error.ErrorCodes.ShouldContain("Granit:DataExchange:Conversion:InvalidFormat");

        // 6. Job reached a terminal state in the store, report persisted
        ImportJob completed = (await jobReader.GetAsync(jobId, cancellationToken))!;
        completed.Status.ShouldBe(ImportJobStatus.PartiallyCompleted);
        completed.Report.ShouldNotBeNull();
        completed.Report!.FailedRows.ShouldBe(1);
        completed.CompletedAt.ShouldNotBeNull();

        // 7. The valid entities were actually INSERTED with correct values
        await using TestAppDbContext appDb = _appFactory.CreateDbContext();
        List<TestEntity> entities = await appDb.TestEntities
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

        entities.Count.ShouldBe(3);
        entities[0].Name.ShouldBe("Alice");
        entities[0].Email.ShouldBe("alice@test.com");
        entities[0].Niss.ShouldBe("111");
        entities[0].Age.ShouldBe(30);
        entities[1].Name.ShouldBe("Bob");
        entities[1].Age.ShouldBe(40);
        entities[2].Name.ShouldBe("Carol");
        entities[2].Niss.ShouldBe("333");
        entities[2].Age.ShouldBe(50);
    }

    [Fact]
    public async Task Dry_run_reports_counts_but_persists_nothing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = _provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        Guid jobId = await UploadAsync(sp, cancellationToken);

        IImportPreviewService previewService = sp.GetRequiredService<IImportPreviewService>();
        ImportPreviewResult preview = (await previewService.PreviewAsync(jobId, cancellationToken))!;

        IImportJobReader jobReader = sp.GetRequiredService<IImportJobReader>();
        IImportJobWriter jobWriter = sp.GetRequiredService<IImportJobWriter>();
        ImportJob job = (await jobReader.GetAsync(jobId, cancellationToken))!;
        job.ConfirmMappings(preview.Suggestions);
        await jobWriter.UpdateAsync(job, job.ConcurrencyStamp, cancellationToken);

        IImportOrchestrator orchestrator = sp.GetRequiredService<IImportOrchestrator>();
        ImportReport report = await orchestrator.DryRunAsync(jobId, cancellationToken);

        report.TotalRows.ShouldBe(5);
        report.SucceededRows.ShouldBe(3);

        // SQLite honors the rollback (the EF InMemory provider silently could not)
        await using TestAppDbContext appDb = _appFactory.CreateDbContext();
        (await appDb.TestEntities.CountAsync(cancellationToken)).ShouldBe(0);

        // Dry-run leaves the job in Mapped state
        ImportJob after = (await jobReader.GetAsync(jobId, cancellationToken))!;
        after.Status.ShouldBe(ImportJobStatus.Mapped);
    }

    [Fact]
    public async Task Reimporting_an_overlapping_file_updates_existing_rows_without_duplicating()
    {
        const string firstCsv = "Name,Email,NISS,Age\nAlice,alice@test.com,111,30\nBob,bob@test.com,222,40\n";
        const string secondCsv = "Name,Email,NISS,Age\nAlice,alice@newdomain.com,111,99\nCarol,carol@test.com,333,50\n";

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (IServiceScope firstScope = _provider.CreateScope())
        {
            ImportReport firstReport = await RunImportAsync(firstScope.ServiceProvider, firstCsv, cancellationToken);
            firstReport.InsertedRows.ShouldBe(2);
            firstReport.UpdatedRows.ShouldBe(0);
        }

        using (IServiceScope secondScope = _provider.CreateScope())
        {
            ImportReport secondReport = await RunImportAsync(secondScope.ServiceProvider, secondCsv, cancellationToken);

            // Alice (NISS 111) already existed → UPDATE; Carol (NISS 333) is brand new → INSERT.
            secondReport.UpdatedRows.ShouldBe(1);
            secondReport.InsertedRows.ShouldBe(1);
            secondReport.FailedRows.ShouldBe(0);
        }

        // Re-read the database: exactly 3 rows (Alice, Bob, Carol) — no duplicate for Alice, and
        // her row was actually updated in place (the historical bug left this silently at 0 rows
        // updated and would have produced a 4th, duplicate Alice row instead).
        await using TestAppDbContext appDb = _appFactory.CreateDbContext();
        List<TestEntity> entities = await appDb.TestEntities.OrderBy(e => e.Name).ToListAsync(cancellationToken);

        entities.Count.ShouldBe(3);
        entities[0].Name.ShouldBe("Alice");
        entities[0].Email.ShouldBe("alice@newdomain.com");
        entities[0].Niss.ShouldBe("111");
        // Age is declared ExcludeOnUpdate on TestImportDefinition — the original value survives the update.
        entities[0].Age.ShouldBe(30);
        entities[1].Name.ShouldBe("Bob");
        entities[2].Name.ShouldBe("Carol");
        entities[2].Age.ShouldBe(50);
    }

    private static async Task<Guid> UploadAsync(IServiceProvider sp, CancellationToken cancellationToken) =>
        await UploadAsync(sp, Csv, cancellationToken);

    private static async Task<Guid> UploadAsync(IServiceProvider sp, string csv, CancellationToken cancellationToken)
    {
        IImportUploadService uploadService = sp.GetRequiredService<IImportUploadService>();
        byte[] bytes = Encoding.UTF8.GetBytes(csv);
        await using MemoryStream stream = new(bytes);

        ImportUploadResult result = await uploadService.UploadAsync(
            "contacts.csv", "text/csv", bytes.Length, stream, DefinitionName, cancellationToken);

        result.Succeeded.ShouldBeTrue(result.ErrorDetail);
        return result.Job!.Id;
    }

    /// <summary>Upload → preview → confirm mappings → execute, in one call, for re-import scenarios.</summary>
    private static async Task<ImportReport> RunImportAsync(IServiceProvider sp, string csv, CancellationToken cancellationToken)
    {
        Guid jobId = await UploadAsync(sp, csv, cancellationToken);

        IImportPreviewService previewService = sp.GetRequiredService<IImportPreviewService>();
        ImportPreviewResult preview = (await previewService.PreviewAsync(jobId, cancellationToken))!;

        IImportJobReader jobReader = sp.GetRequiredService<IImportJobReader>();
        IImportJobWriter jobWriter = sp.GetRequiredService<IImportJobWriter>();
        ImportJob job = (await jobReader.GetAsync(jobId, cancellationToken))!;
        job.ConfirmMappings(preview.Suggestions);
        await jobWriter.UpdateAsync(job, job.ConcurrencyStamp, cancellationToken);

        IImportOrchestrator orchestrator = sp.GetRequiredService<IImportOrchestrator>();
        return await orchestrator.ExecuteAsync(jobId, cancellationToken);
    }

    // ── Test infrastructure ──────────────────────────────────────────────────

    /// <summary>
    /// Adapts the house <see cref="SqliteDbContextFactory{TContext}"/> (which cannot know about
    /// <see cref="IDbContextFactory{TContext}"/>'s create-per-call contract) to the factory
    /// interface the import executor consumes. Schema is created once by the test constructor.
    /// </summary>
    private sealed class SqliteAppContextFactory(SqliteDbContextFactory<TestAppDbContext> inner)
        : IDbContextFactory<TestAppDbContext>
    {
        public TestAppDbContext CreateDbContext() => inner.CreateContext(ensureCreated: false);
    }

    /// <summary>
    /// SQLite factory for the internal <see cref="DataExchangeDbContext"/>. The house
    /// <see cref="SqliteDbContextFactory{TContext}"/> cannot construct it — its constructor
    /// requires an <see cref="ICurrentTenant"/> — so this mirrors the pattern
    /// with <see cref="GranitDesignTime.CurrentTenant"/>.
    /// </summary>
    private sealed class SqliteDataExchangeContextFactory : IDbContextFactory<DataExchangeDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly DbContextOptions<DataExchangeDbContext> _options;

        public SqliteDataExchangeContextFactory()
        {
            _connection.Open();
            // ConcurrencyStampInterceptor mirrors production: the stamped UpdateAsync
            // overload rejects empty stamps.
            _options = new DbContextOptionsBuilder<DataExchangeDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(new ConcurrencyStampInterceptor())
                .Options;

            using DataExchangeDbContext context = CreateDbContext();
            context.Database.EnsureCreated();
        }

        public DataExchangeDbContext CreateDbContext() => new(_options, GranitDesignTime.CurrentTenant);

        public void Dispose() => _connection.Dispose();
    }

    /// <summary>
    /// Test-local in-memory file provider — the production one is internal to the base
    /// module and registered scoped, so it cannot back a multi-scope flow here.
    /// </summary>
    private sealed class InMemoryFileProvider : IDataExchangeFileProvider
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
        private int _next;

        public Task<Stream> OpenAsync(BlobReference blobReference, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(_files[blobReference.Value], writable: false));

        public async Task<BlobReference> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
        {
            await using MemoryStream buffer = new();
            await content.CopyToAsync(buffer, cancellationToken);
            string key = $"blob-{++_next}";
            _files[key] = buffer.ToArray();
            return BlobReference.Create(key);
        }

        public async Task<BlobReference> SaveAsync(
            string fileName,
            string contentType,
            Func<Stream, CancellationToken, Task> writeAsync,
            CancellationToken cancellationToken = default)
        {
            await using MemoryStream buffer = new();
            await writeAsync(buffer, cancellationToken);
            string key = $"blob-{++_next}";
            _files[key] = buffer.ToArray();
            return BlobReference.Create(key);
        }

        public Task DeleteAsync(BlobReference blobReference, CancellationToken cancellationToken = default)
        {
            _files.Remove(blobReference.Value);
            return Task.CompletedTask;
        }
    }
}
