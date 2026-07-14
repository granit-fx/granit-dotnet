using Granit.DataExchange.BackgroundJobs.Options;
using Granit.DataExchange.BackgroundJobs.Services;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Reporting;
using Granit.DataExchange.Retention;
using Granit.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.BackgroundJobs.Tests.Services;

public sealed class RetentionSweepServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantA = Guid.NewGuid();

    private readonly IDataExchangeRetentionStore _store = Substitute.For<IDataExchangeRetentionStore>();
    private readonly IDataExchangeFileProvider _fileProvider = Substitute.For<IDataExchangeFileProvider>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly ServiceProvider _sp;
    private readonly DataExchangeMetrics _metrics;

    public RetentionSweepServiceTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new DataExchangeMetrics(_sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
    }

    public void Dispose() => _sp.Dispose();

    private RetentionSweepService CreateService(DataExchangeRetentionOptions? options = null) =>
        new(_store, _fileProvider, _timeProvider, _metrics,
            Microsoft.Extensions.Options.Options.Create(options ?? new DataExchangeRetentionOptions()),
            NullLogger<RetentionSweepService>.Instance);

    // ── Import file purge ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ExpiredImportFile_DeletesFileMarksDeletedAndUpdates()
    {
        ImportJob job = CreateCompletedImportJob(TenantA);
        _store.GetImportJobsWithExpiredFilesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([job]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _fileProvider.Received(1).DeleteAsync(job.BlobReference, Arg.Any<CancellationToken>());
        job.FileDeletedAt.ShouldNotBeNull();
        await _store.Received(1).UpdateImportJobAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredImportFile_RecordsRetentionPurgedMetric()
    {
        ImportJob job = CreateCompletedImportJob(TenantA);
        _store.GetImportJobsWithExpiredFilesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([job]);

        using MeterListenerHarness harness = new(DataExchangeMetrics.MeterName);
        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        harness.SumFor("granit.dataexchange.retention.purged", "kind", "import_file").ShouldBe(1);
    }

    // ── Export file purge ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ExpiredExportFile_DeletesFileMarksDeletedAndUpdates()
    {
        ExportJob job = CreateCompletedExportJob(TenantA);
        _store.GetExportJobsWithExpiredFilesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([job]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _fileProvider.Received(1).DeleteAsync(job.BlobReference!, Arg.Any<CancellationToken>());
        job.FileDeletedAt.ShouldNotBeNull();
        await _store.Received(1).UpdateExportJobAsync(job, Arg.Any<CancellationToken>());
    }

    // ── Stuck job recovery ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_StuckImportJob_CompletesAsFailedAndUpdates()
    {
        ImportJob job = CreateExecutingImportJob(TenantA);
        _store.GetStuckImportJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([job]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        job.Status.ShouldBe(ImportJobStatus.Failed);
        job.Report.ShouldNotBeNull();
        job.Report!.RowErrors.ShouldHaveSingleItem();
        await _store.Received(1).UpdateImportJobAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_StuckExportJob_FailsAndUpdates()
    {
        ExportJob job = CreateExportingJob(TenantA);
        _store.GetStuckExportJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([job]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        job.Status.ShouldBe(ExportJobStatus.Failed);
        job.ErrorMessage.ShouldNotBeNullOrEmpty();
        await _store.Received(1).UpdateExportJobAsync(job, Arg.Any<CancellationToken>());
    }

    // ── Record purge ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ExpiredTerminalImportRecord_DeletesRecord()
    {
        ImportJob job = CreateCompletedImportJob(TenantA);
        _store.GetExpiredTerminalImportJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([job]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _store.Received(1).DeleteImportJobAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredTerminalExportRecord_DeletesRecord()
    {
        ExportJob job = CreateCompletedExportJob(TenantA);
        _store.GetExpiredTerminalExportJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([job]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _store.Received(1).DeleteExportJobAsync(job, Arg.Any<CancellationToken>());
    }

    // ── Resilience ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenOneFileDeleteThrows_ContinuesProcessingOtherJobs()
    {
        ImportJob failingJob = CreateCompletedImportJob(TenantA);
        ImportJob healthyJob = CreateCompletedImportJob(TenantA);
        _store.GetImportJobsWithExpiredFilesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([failingJob, healthyJob]);
        _fileProvider.DeleteAsync(failingJob.BlobReference, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("blob storage unavailable"));

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        failingJob.FileDeletedAt.ShouldBeNull();
        await _store.DidNotReceive().UpdateImportJobAsync(failingJob, Arg.Any<CancellationToken>());

        healthyJob.FileDeletedAt.ShouldNotBeNull();
        await _store.Received(1).UpdateImportJobAsync(healthyJob, Arg.Any<CancellationToken>());
    }

    // ── Fixtures ──────────────────────────────────────────────────────────

    private static ImportJob CreateExecutingImportJob(Guid tenantId)
    {
        var job = ImportJob.Create(
            Guid.NewGuid(), "Test.ImportDefinition", "TestEntity", "data.csv", "text/csv", 1024,
            BlobReference.Create("import/blob-" + Guid.NewGuid()), tenantId);
        job.MarkAsPreviewed();
        job.ConfirmMappings([]);
        job.MarkAsExecuting();
        return job;
    }

    private static ImportJob CreateCompletedImportJob(Guid tenantId)
    {
        ImportJob job = CreateExecutingImportJob(tenantId);
        job.Complete(
            ImportJobStatus.Completed,
            new ImportReport
            {
                TotalRows = 10,
                SucceededRows = 10,
                FailedRows = 0,
                SkippedRows = 0,
                InsertedRows = 10,
                UpdatedRows = 0,
                Duration = TimeSpan.FromSeconds(1),
                FinalStatus = ImportJobStatus.Completed,
                RowErrors = [],
            },
            Now.AddDays(-60));
        return job;
    }

    private static ExportJob CreateExportingJob(Guid tenantId)
    {
        var job = ExportJob.Create(
            Guid.NewGuid(), "Test.ExportDefinition", "csv",
            new ExportRequest("Test.ExportDefinition", "csv", null, false, null, null, null, null), tenantId);
        job.MarkAsExporting();
        return job;
    }

    private static ExportJob CreateCompletedExportJob(Guid tenantId)
    {
        ExportJob job = CreateExportingJob(tenantId);
        job.Complete(BlobReference.Create("export/blob-" + Guid.NewGuid()), "export.csv", 10, Now.AddDays(-30));
        return job;
    }
}
