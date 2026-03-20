using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Granit.Core.Events;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Export.Internal;
using Granit.DataExchange.Export.Messages;
using Granit.DataExchange.Import.Pipeline;
using Granit.Guids;
using Granit.Querying;
using Granit.Querying.Meta;
using Granit.Querying.SavedViews;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class ExportOrchestratorTests
{
    private readonly IExportJobReader _jobReader = Substitute.For<IExportJobReader>();
    private readonly IExportJobWriter _jobWriter = Substitute.For<IExportJobWriter>();
    private readonly IExportCommandDispatcher _dispatcher = Substitute.For<IExportCommandDispatcher>();
    private readonly IImportFileProvider _fileProvider = Substitute.For<IImportFileProvider>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ILocalEventBus _eventBus = Substitute.For<ILocalEventBus>();
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly DataExchangeMetrics _metrics = new(new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>());
    private readonly DateTimeOffset _now = new(2026, 3, 3, 10, 0, 0, TimeSpan.Zero);

    public ExportOrchestratorTests()
    {
        _clock.Now.Returns(_now);

        _fileProvider.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("blob-ref-export");

        _jobReader.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                // Return a job matching the requested ID with Queued status by default
                Guid id = call.Arg<Guid>();
                return ExportJob.Create(
                    id,
                    "Test.Export",
                    "csv",
                    JsonSerializer.Serialize(new ExportRequest(
                        "Test.Export", "csv", null, false, null, null, null, null)));
            });
    }

    // ── ExportAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_creates_job_and_dispatches()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        ExportRequest request = new("Test.Export", "csv", null, false, null, null, null, null);

        // Act
        ExportJobResult result = await sut.ExportAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ExportJobStatus.Queued);
        result.JobId.ShouldNotBe(Guid.Empty);
        await _jobWriter.Received(1).CreateAsync(
            Arg.Is<ExportJob>(j => j.DefinitionName == "Test.Export" && j.Format == "csv"),
            Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<ExecuteExportCommand>(c => c.ExportJobId == result.JobId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_unknown_definition_throws()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        ExportRequest request = new("Unknown.Export", "csv", null, false, null, null, null, null);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.ExportAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExportAsync_unknown_format_throws()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        ExportRequest request = new("Test.Export", "pdf", null, false, null, null, null, null);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.ExportAsync(request, TestContext.Current.CancellationToken));
    }

    // ── ExecuteAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_processes_rows_and_completes_job()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — job is mutated in-place by the orchestrator
        job.Status.ShouldBe(ExportJobStatus.Completed);
        job.RowCount.ShouldBe(2); // TestDataSource yields 2 entities
        job.BlobReference.ShouldBe("blob-ref-export");
        job.FileName.ShouldNotBeNull();
        job.CompletedAt.ShouldBe(_now);
        // 2 calls: Exporting then Completed (same reference, so check call count)
        await _jobWriter.Received(2).UpdateAsync(Arg.Any<ExportJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_unknown_job_returns_silently()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ExportJob?)null);

        // Act — should not throw
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        await _jobWriter.DidNotReceive().UpdateAsync(Arg.Any<ExportJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_with_selected_fields_filters_columns()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.Export", "csv", ["Name"], false, null, null, null, null);
        var job = ExportJob.Create(jobId, "Test.Export", "csv", JsonSerializer.Serialize(request));
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter capturedWriter = Substitute.For<IExportWriter>();
        capturedWriter.CanWrite("csv").Returns(true);
        capturedWriter.FileExtension.Returns(".csv");
        capturedWriter.MimeType.Returns("text/csv");

        IReadOnlyList<ExportFieldDescriptor>? capturedFields = null;
        capturedWriter.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(),
            Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedFields = call.Arg<IReadOnlyList<ExportFieldDescriptor>>();
                return Task.CompletedTask;
            });

        ExportOrchestrator sutWithCapture = CreateOrchestrator(capturedWriter);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        await sutWithCapture.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        capturedFields.ShouldNotBeNull();
        capturedFields!.Count.ShouldBe(1);
        capturedFields[0].PropertyPath.ShouldBe("Name");
    }

    [Fact]
    public async Task ExecuteAsync_with_navigation_field_resolves_dot_notation()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.Export", "csv", ["Company.Name"], false, null, null, null, null);
        var job = ExportJob.Create(jobId, "Test.Export", "csv", JsonSerializer.Serialize(request));
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        List<IReadOnlyDictionary<string, object?>> capturedRows = [];
        IExportWriter capturedWriter = Substitute.For<IExportWriter>();
        capturedWriter.CanWrite("csv").Returns(true);
        capturedWriter.FileExtension.Returns(".csv");
        capturedWriter.MimeType.Returns("text/csv");
        capturedWriter.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(),
            Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows =
                    call.Arg<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>();
                await foreach (IReadOnlyDictionary<string, object?> row in rows)
                {
                    capturedRows.Add(row);
                }
            });

        ExportOrchestrator sutWithCapture = CreateOrchestrator(capturedWriter);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        await sutWithCapture.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        capturedRows.Count.ShouldBe(2);
        capturedRows[0]["Company.Name"].ShouldBe("Acme Corp");
        capturedRows[1]["Company.Name"].ShouldBeNull(); // Jane has no company
    }

    [Fact]
    public async Task ExecuteAsync_failure_sets_job_to_failed()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter failingWriter = Substitute.For<IExportWriter>();
        failingWriter.CanWrite("csv").Returns(true);
        failingWriter.FileExtension.Returns(".csv");
        failingWriter.MimeType.Returns("text/csv");
        failingWriter.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("Disk full"));

        ExportOrchestrator sut = CreateOrchestrator(failingWriter);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act & Assert
        await Should.ThrowAsync<IOException>(
            () => sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken));

        job.Status.ShouldBe(ExportJobStatus.Failed);
        job.ErrorMessage.ShouldBe("Disk full");
        job.CompletedAt.ShouldBe(_now);
    }

    [Fact]
    public async Task ExecuteAsync_WithQueryDefinition_UsesQueryEngine()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.QueryExport", "csv", null, false, "-Name", null, null, null);
        var job = ExportJob.Create(jobId, "Test.QueryExport", "csv", JsonSerializer.Serialize(request));
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        FakeQueryEngine queryEngine = new();
        ExportOrchestrator sut = CreateOrchestratorWithQueryEngine(queryEngine);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — the query engine should have been called
        queryEngine.StreamCalled.ShouldBeTrue();
        queryEngine.CapturedRequest.ShouldNotBeNull();
        queryEngine.CapturedRequest!.Sort.ShouldBe("-Name");
        job.Status.ShouldBe(ExportJobStatus.Completed);
        job.RowCount.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_with_empty_selected_fields_returns_all()
    {
        // Arrange — empty list (not null) should behave like null: return all fields
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.Export", "csv", [], false, null, null, null, null);
        var job = ExportJob.Create(jobId, "Test.Export", "csv", JsonSerializer.Serialize(request));
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IReadOnlyList<ExportFieldDescriptor>? capturedFields = null;
        IExportWriter capturedWriter = Substitute.For<IExportWriter>();
        capturedWriter.CanWrite("csv").Returns(true);
        capturedWriter.FileExtension.Returns(".csv");
        capturedWriter.MimeType.Returns("text/csv");
        capturedWriter.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(),
            Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedFields = call.Arg<IReadOnlyList<ExportFieldDescriptor>>();
                return Task.CompletedTask;
            });

        ExportOrchestrator sutWithCapture = CreateOrchestrator(capturedWriter);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        await sutWithCapture.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — all 3 fields from TestExportDefinition
        capturedFields.ShouldNotBeNull();
        capturedFields!.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteAsync_with_selected_fields_preserves_user_order()
    {
        // Arrange — user asks for Email before Name
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.Export", "csv", ["Email", "Name"], false, null, null, null, null);
        var job = ExportJob.Create(jobId, "Test.Export", "csv", JsonSerializer.Serialize(request));
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IReadOnlyList<ExportFieldDescriptor>? capturedFields = null;
        IExportWriter capturedWriter = Substitute.For<IExportWriter>();
        capturedWriter.CanWrite("csv").Returns(true);
        capturedWriter.FileExtension.Returns(".csv");
        capturedWriter.MimeType.Returns("text/csv");
        capturedWriter.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(),
            Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedFields = call.Arg<IReadOnlyList<ExportFieldDescriptor>>();
                return Task.CompletedTask;
            });

        ExportOrchestrator sutWithCapture = CreateOrchestrator(capturedWriter);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        await sutWithCapture.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — order preserved as user specified
        capturedFields.ShouldNotBeNull();
        capturedFields!.Count.ShouldBe(2);
        capturedFields[0].PropertyPath.ShouldBe("Email");
        capturedFields[1].PropertyPath.ShouldBe("Name");
    }

    [Fact]
    public async Task ExecuteAsync_filename_contains_sanitized_definition_name()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — definition name "Test.Export" becomes "Test_Export" in filename
        job.FileName.ShouldNotBeNull();
        job.FileName!.ShouldStartWith("Test_Export_");
        job.FileName.ShouldEndWith(".csv");
        // The dot in "Test.Export" should be sanitized to underscore
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(job.FileName);
        nameWithoutExtension.ShouldNotContain(".");
    }

    [Fact]
    public async Task ExecuteAsync_WithQueryDefinition_passes_filter_and_search()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        Dictionary<string, string> filter = new() { ["name.eq"] = "Alice" };
        Dictionary<string, string> presets = new() { ["status"] = "Active" };
        ExportRequest request = new("Test.QueryExport", "csv", null, false, "-Name", filter, presets, "test search");
        var job = ExportJob.Create(jobId, "Test.QueryExport", "csv", JsonSerializer.Serialize(request));
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        FakeQueryEngine queryEngine = new();
        ExportOrchestrator sut = CreateOrchestratorWithQueryEngine(queryEngine);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — all query params forwarded
        queryEngine.StreamCalled.ShouldBeTrue();
        queryEngine.CapturedRequest.ShouldNotBeNull();
        queryEngine.CapturedRequest!.Sort.ShouldBe("-Name");
        queryEngine.CapturedRequest.Search.ShouldBe("test search");
        queryEngine.CapturedRequest.Filter.ShouldNotBeNull();
        queryEngine.CapturedRequest.Filter!["name.eq"].ShouldBe("Alice");
        queryEngine.CapturedRequest.Presets.ShouldNotBeNull();
        queryEngine.CapturedRequest.Presets!["status"].ShouldBe("Active");
    }

    [Fact]
    public async Task ExecuteAsync_completed_publishes_ExportJobCompletedEvent()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        job.CreatedBy = "user-42";
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<ExportJobCompletedEvent>(e =>
                e.ExportJobId == jobId &&
                e.DefinitionName == "Test.Export" &&
                e.Status == ExportJobStatus.Completed &&
                e.UserId == "user-42" &&
                e.RowCount == 2 &&
                e.ErrorMessage == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_failed_publishes_ExportJobCompletedEvent_with_error()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        job.CreatedBy = "user-99";
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter failingWriter = Substitute.For<IExportWriter>();
        failingWriter.CanWrite("csv").Returns(true);
        failingWriter.FileExtension.Returns(".csv");
        failingWriter.MimeType.Returns("text/csv");
        failingWriter.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("Disk full"));

        ExportOrchestrator sut = CreateOrchestrator(failingWriter);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act & Assert
        await Should.ThrowAsync<IOException>(
            () => sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken));

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<ExportJobCompletedEvent>(e =>
                e.ExportJobId == jobId &&
                e.Status == ExportJobStatus.Failed &&
                e.UserId == "user-99" &&
                e.RowCount == null &&
                e.ErrorMessage == "Disk full"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_OperationCanceledException_propagates_without_catch()
    {
        // Arrange — OperationCanceledException should NOT be caught by the error handler
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter cancelWriter = Substitute.For<IExportWriter>();
        cancelWriter.CanWrite("csv").Returns(true);
        cancelWriter.FileExtension.Returns(".csv");
        cancelWriter.MimeType.Returns("text/csv");
        cancelWriter.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new OperationCanceledException());

        ExportOrchestrator sut = CreateOrchestrator(cancelWriter);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act & Assert — should propagate, NOT set job to Failed
        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken));

        job.Status.ShouldNotBe(ExportJobStatus.Failed);
    }

    [Fact]
    public async Task GetDownloadAsync_completed_job_xlsx_returns_correct_mime_type()
    {
        // Arrange — xlsx format
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        var job = ExportJob.Create(jobId, "Test.Export", "xlsx", "{}");
        job.Complete("blob-ref-xlsx", "test_export.xlsx", 0, DateTimeOffset.UtcNow);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter xlsxWriter = Substitute.For<IExportWriter>();
        xlsxWriter.CanWrite("xlsx").Returns(true);
        xlsxWriter.MimeType.Returns("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        MemoryStream blobStream = new([1, 2, 3]);
        _fileProvider.OpenAsync("blob-ref-xlsx", Arg.Any<CancellationToken>())
            .Returns(blobStream);

        ServiceCollection services = new();
        services.AddSingleton<IExportDefinitionDescriptor>(new TestExportDefinition());
        services.AddSingleton<IExportDataSource<TestEntity>>(new TestDataSource());
        services.AddSingleton(Options.Create(new ExportOptions()));
        ServiceProvider sp = services.BuildServiceProvider();

        ExportOrchestrator sutXlsx = new(
            sp,
            [xlsxWriter],
            _jobReader,
            _jobWriter,
            _dispatcher,
            _fileProvider,
            _clock,
            new SimpleGuidGenerator(),
            _eventBus,
            _distributedEventBus,
            _metrics,
            NullLogger<ExportOrchestrator>.Instance);

        // Act
        ExportDownload? download = await sutXlsx.GetDownloadAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        download.ShouldNotBeNull();
        download!.MimeType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        download.FileName.ShouldBe("test_export.xlsx");
    }

    // ── GetJobAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetJobAsync_delegates_to_store()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        ExportJob expected = BuildJob(jobId, ExportJobStatus.Completed);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        ExportJob? result = await sut.GetJobAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(expected);
    }

    // ── GetDownloadAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetDownloadAsync_completed_job_returns_download()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        var job = ExportJob.Create(jobId, "Test.Export", "csv",
            JsonSerializer.Serialize(new ExportRequest("Test.Export", "csv", null, false, null, null, null, null)));
        job.Complete("blob-ref-export", "test_export.csv", 10, DateTimeOffset.UtcNow);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        MemoryStream blobStream = new([1, 2, 3]);
        _fileProvider.OpenAsync("blob-ref-export", Arg.Any<CancellationToken>())
            .Returns(blobStream);

        // Act
        ExportDownload? download = await sut.GetDownloadAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        download.ShouldNotBeNull();
        download!.MimeType.ShouldBe("text/csv");
        download.FileName.ShouldBe("test_export.csv");
    }

    [Fact]
    public async Task GetDownloadAsync_non_completed_job_returns_null()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Exporting);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        ExportDownload? download = await sut.GetDownloadAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        download.ShouldBeNull();
    }

    [Fact]
    public async Task GetDownloadAsync_unknown_job_returns_null()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ExportJob?)null);

        // Act
        ExportDownload? download = await sut.GetDownloadAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        download.ShouldBeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private ExportOrchestrator CreateOrchestrator(IExportWriter? writerOverride = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IExportDefinitionDescriptor>(new TestExportDefinition());
        services.AddSingleton<IExportDataSource<TestEntity>>(new TestDataSource());
        services.AddSingleton(Options.Create(new ExportOptions()));

        IExportWriter writer = writerOverride ?? CreateCsvWriter();
        ServiceProvider sp = services.BuildServiceProvider();

        return new ExportOrchestrator(
            sp,
            [writer],
            _jobReader,
            _jobWriter,
            _dispatcher,
            _fileProvider,
            _clock,
            new SimpleGuidGenerator(),
            _eventBus,
            _distributedEventBus,
            _metrics,
            NullLogger<ExportOrchestrator>.Instance);
    }

    private ExportOrchestrator CreateOrchestratorWithQueryEngine(
        IQueryEngine<TestEntity> queryEngine,
        IExportWriter? writerOverride = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IExportDefinitionDescriptor>(new TestQueryExportDefinition());
        services.AddSingleton<IExportDataSource<TestEntity>>(new TestDataSource());
        services.AddSingleton(queryEngine);
        services.AddSingleton(Options.Create(new ExportOptions()));

        IExportWriter writer = writerOverride ?? CreateCsvWriter();
        ServiceProvider sp = services.BuildServiceProvider();

        return new ExportOrchestrator(
            sp,
            [writer],
            _jobReader,
            _jobWriter,
            _dispatcher,
            _fileProvider,
            _clock,
            new SimpleGuidGenerator(),
            _eventBus,
            _distributedEventBus,
            _metrics,
            NullLogger<ExportOrchestrator>.Instance);
    }

    private static IExportWriter CreateCsvWriter()
    {
        IExportWriter writer = Substitute.For<IExportWriter>();
        writer.CanWrite("csv").Returns(true);
        writer.FileExtension.Returns(".csv");
        writer.MimeType.Returns("text/csv");
        // The writer must consume the async enumerable so the orchestrator can count rows
        writer.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>(),
            Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows =
                    call.Arg<IAsyncEnumerable<IReadOnlyDictionary<string, object?>>>();
                await foreach (IReadOnlyDictionary<string, object?> _ in rows)
                {
                    // consume all rows
                }
            });
        return writer;
    }

    private static ExportJob BuildJob(Guid id, ExportJobStatus status)
    {
        var job = ExportJob.Create(
            id,
            "Test.Export",
            "csv",
            JsonSerializer.Serialize(new ExportRequest(
                "Test.Export", "csv", null, false, null, null, null, null)));

        // Transition to desired status using behavior methods
        if (status == ExportJobStatus.Exporting)
        {
            job.MarkAsExporting();
        }
        else if (status == ExportJobStatus.Completed)
        {
            job.Complete("blob-ref", "export.csv", 0, DateTimeOffset.UtcNow);
        }
        else if (status == ExportJobStatus.Failed)
        {
            job.Fail("failed", DateTimeOffset.UtcNow);
        }

        return job;
    }

    // ── Test types ──────────────────────────────────────────────────────

    private sealed class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public TestCompany? Company { get; set; }
    }

    private sealed class TestCompany
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestExportDefinition : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.Export";

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder) =>
            builder
                .Field(e => e.Name, f => f.Header("Nom"))
                .Field(e => e.Email)
                .Field(e => e.Company, c => c.Name, f => f.Header("Société"));
    }

    private sealed class TestQueryExportDefinition : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.QueryExport";
        public override string? QueryDefinitionName => "Test.Entities";

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder) =>
            builder
                .Field(e => e.Name, f => f.Header("Nom"))
                .Field(e => e.Email);
    }

    private sealed class FakeQueryEngine : IQueryEngine<TestEntity>
    {
        public bool StreamCalled { get; private set; }
        public QueryRequest? CapturedRequest { get; private set; }

        public async IAsyncEnumerable<TestEntity> ExecuteStreamAsync(
            IQueryable<TestEntity> source,
            QueryRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamCalled = true;
            CapturedRequest = request;
            await Task.CompletedTask.ConfigureAwait(false);
            foreach (TestEntity item in source)
            {
                yield return item;
            }
        }

        public Task<PagedResult<TestEntity>> ExecuteAsync(
            IQueryable<TestEntity> source, QueryRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GroupedResult<TestEntity>> ExecuteGroupedAsync(
            IQueryable<TestEntity> source, QueryRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public QueryMetadata GetMetadata(IReadOnlyList<SavedViewSummary>? savedViews = null) =>
            throw new NotSupportedException();
    }

    private sealed class TestDataSource : IExportDataSource<TestEntity>
    {
        public IQueryable<TestEntity> GetQueryable() =>
            new List<TestEntity>
            {
                new()
                {
                    Name = "Alice",
                    Email = "alice@test.com",
                    Company = new TestCompany { Name = "Acme Corp" },
                },
                new()
                {
                    Name = "Jane",
                    Email = "jane@test.com",
                    Company = null,
                },
            }.AsQueryable();
    }
}
