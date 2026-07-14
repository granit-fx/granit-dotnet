using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Granit.Commands;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Export.Exceptions;
using Granit.DataExchange.Export.Internal;
using Granit.DataExchange.Export.Messages;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.QueryEngine.Meta;
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
    private readonly ICommandSender _commandSender = Substitute.For<ICommandSender>();
    private readonly IDataExchangeFileProvider _fileProvider = Substitute.For<IDataExchangeFileProvider>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly DataExchangeMetrics _metrics = new(new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>());
    private readonly DateTimeOffset _now = new(2026, 3, 3, 10, 0, 0, TimeSpan.Zero);

    public ExportOrchestratorTests()
    {
        _clock.Now.Returns(_now);

        _fileProvider.SaveAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Func<Stream, CancellationToken, Task>>(),
            Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                // Mirror the real providers: invoke the streaming callback against a throwaway
                // stream so the pipeline (and its row count) actually runs, then hand back the reference.
                Func<Stream, CancellationToken, Task> writeAsync = call.Arg<Func<Stream, CancellationToken, Task>>();
                CancellationToken cancellationToken = call.Arg<CancellationToken>();
                await using MemoryStream stream = new();
                await writeAsync(stream, cancellationToken);
                return Granit.Domain.ValueObjects.BlobReference.Create("blob-ref-export");
            });

        _jobReader.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                // Return a job matching the requested ID with Queued status by default
                Guid id = call.Arg<Guid>();
                return ExportJob.Create(
                    id,
                    "Test.Export",
                    "csv",
                    new ExportRequest("Test.Export", "csv", null, false, null, null, null, null));
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
        await _commandSender.Received(1).SendAsync(
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
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.ExportAsync(request, TestContext.Current.CancellationToken));

        // The miss message lists the registered definition names
        ex.Message.ShouldContain("Unknown.Export");
        ex.Message.ShouldContain("Test.Export");
    }

    [Fact]
    public async Task ExportAsync_definition_lookup_is_case_sensitive()
    {
        // Arrange — registry lookups are ordinal (matching the import side)
        ExportOrchestrator sut = CreateOrchestrator();
        ExportRequest request = new("test.export", "csv", null, false, null, null, null, null);

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
        job.BlobReference!.Value.ShouldBe("blob-ref-export");
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
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.Export", "csv", ["Name"], false, null, null, null, null);
        var job = ExportJob.Create(jobId, "Test.Export", "csv", request);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IReadOnlyList<ExportFieldDescriptor>? capturedFields = null;
        IExportWriter capturedWriter = CreateCsvWriter(captureFields: f => capturedFields = f);
        ExportOrchestrator sut = CreateOrchestrator(capturedWriter);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        capturedFields.ShouldNotBeNull();
        capturedFields!.Count.ShouldBe(1);
        capturedFields[0].PropertyPath.ShouldBe("Name");
    }

    [Fact]
    public async Task ExecuteAsync_with_navigation_field_resolves_dot_notation()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.Export", "csv", ["Company.Name"], false, null, null, null, null);
        var job = ExportJob.Create(jobId, "Test.Export", "csv", request);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        List<object?[]> capturedRows = [];
        IExportWriter capturedWriter = CreateCsvWriter(captureRows: capturedRows);
        ExportOrchestrator sut = CreateOrchestrator(capturedWriter);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — single selected field "Company.Name" at index 0
        capturedRows.Count.ShouldBe(2);
        capturedRows[0][0].ShouldBe("Acme Corp");
        capturedRows[1][0].ShouldBeNull(); // Jane has no company — null-propagating compiled getter
    }

    [Fact]
    public async Task ExecuteAsync_failure_sets_job_to_failed()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter failingWriter = CreateFailingWriter(new IOException("Disk full"));
        ExportOrchestrator sut = CreateOrchestrator(failingWriter);

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
        var job = ExportJob.Create(jobId, "Test.QueryExport", "csv", request);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        FakeQueryEngine queryEngine = new();
        ExportOrchestrator sut = CreateOrchestrator(
            definition: new TestQueryExportDefinition(),
            queryEngine: queryEngine);

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
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.Export", "csv", [], false, null, null, null, null);
        var job = ExportJob.Create(jobId, "Test.Export", "csv", request);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IReadOnlyList<ExportFieldDescriptor>? capturedFields = null;
        IExportWriter capturedWriter = CreateCsvWriter(captureFields: f => capturedFields = f);
        ExportOrchestrator sut = CreateOrchestrator(capturedWriter);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — all 3 fields from TestExportDefinition
        capturedFields.ShouldNotBeNull();
        capturedFields!.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteAsync_with_selected_fields_preserves_user_order()
    {
        // Arrange — user asks for Email before Name
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.Export", "csv", ["Email", "Name"], false, null, null, null, null);
        var job = ExportJob.Create(jobId, "Test.Export", "csv", request);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IReadOnlyList<ExportFieldDescriptor>? capturedFields = null;
        IExportWriter capturedWriter = CreateCsvWriter(captureFields: f => capturedFields = f);
        ExportOrchestrator sut = CreateOrchestrator(capturedWriter);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

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

        // Assert — definition name "Test.Export" is preserved (dots are valid in filenames)
        // and invalid chars (from Path.GetInvalidFileNameChars()) are replaced with underscores
        job.FileName.ShouldNotBeNull();
        job.FileName!.ShouldStartWith("Test.Export_");
        job.FileName.ShouldEndWith(".csv");
    }

    [Fact]
    public async Task ExecuteAsync_WithQueryDefinition_passes_filter_and_search()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        Dictionary<string, string> filter = new() { ["name.eq"] = "Alice" };
        Dictionary<string, string> presets = new() { ["status"] = "Active" };
        ExportRequest request = new("Test.QueryExport", "csv", null, false, "-Name", filter, presets, "test search");
        var job = ExportJob.Create(jobId, "Test.QueryExport", "csv", request);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        FakeQueryEngine queryEngine = new();
        ExportOrchestrator sut = CreateOrchestrator(
            definition: new TestQueryExportDefinition(),
            queryEngine: queryEngine);

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
    public async Task ExecuteAsync_completed_buffers_ExportJobCompletedEto_on_the_aggregate()
    {
        // Arrange
        ExportOrchestrator sut = CreateOrchestrator();
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        job.CreatedBy = "user-42";
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — Complete() buffers the Eto on the aggregate itself (transactional outbox);
        // no event bus is involved — DomainEventDispatcherInterceptor drains IntegrationEvents
        // pre-commit on the next SaveChanges.
        ExportJobCompletedEto eto = job.IntegrationEvents.OfType<ExportJobCompletedEto>().ShouldHaveSingleItem();
        eto.ExportJobId.ShouldBe(jobId);
        eto.DefinitionName.ShouldBe("Test.Export");
        eto.Status.ShouldBe(ExportJobStatus.Completed);
        eto.UserId.ShouldBe("user-42");
        eto.RowCount.ShouldBe(2);
        eto.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_failed_buffers_ExportJobCompletedEto_with_error_on_the_aggregate()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        job.CreatedBy = "user-99";
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter failingWriter = CreateFailingWriter(new IOException("Disk full"));
        ExportOrchestrator sut = CreateOrchestrator(failingWriter);

        // Act & Assert
        await Should.ThrowAsync<IOException>(
            () => sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken));

        // Assert — the failure Eto folds into the same ExportJobCompletedEto (Status
        // disambiguates); the former dedicated ExportJobFailedEto no longer exists.
        ExportJobCompletedEto eto = job.IntegrationEvents.OfType<ExportJobCompletedEto>().ShouldHaveSingleItem();
        eto.ExportJobId.ShouldBe(jobId);
        eto.Status.ShouldBe(ExportJobStatus.Failed);
        eto.UserId.ShouldBe("user-99");
        eto.RowCount.ShouldBeNull();
        eto.ErrorMessage.ShouldBe("Disk full");
    }

    [Fact]
    public async Task ExecuteAsync_redelivered_completed_job_returns_without_reexecuting()
    {
        // Arrange — simulates Wolverine at-least-once redelivery of a command whose job already
        // reached a terminal state on a prior delivery.
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Completed);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter writer = CreateCsvWriter();
        ExportOrchestrator sut = CreateOrchestrator(writer);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — no re-execution, no throw
        job.Status.ShouldBe(ExportJobStatus.Completed);
        await writer.DidNotReceive().WriteAsync(
            Arg.Any<Stream>(), Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<object?[]>>(), Arg.Any<CancellationToken>());
        await _jobWriter.DidNotReceive().UpdateAsync(Arg.Any<ExportJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_redelivered_failed_job_returns_without_reexecuting()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Failed);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter writer = CreateCsvWriter();
        ExportOrchestrator sut = CreateOrchestrator(writer);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — no re-execution, no throw
        job.Status.ShouldBe(ExportJobStatus.Failed);
        await writer.DidNotReceive().WriteAsync(
            Arg.Any<Stream>(), Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<object?[]>>(), Arg.Any<CancellationToken>());
        await _jobWriter.DidNotReceive().UpdateAsync(Arg.Any<ExportJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_job_already_exporting_returns_without_reexecuting()
    {
        // Arrange — another (or an earlier in-flight) delivery already owns this run.
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Exporting);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter writer = CreateCsvWriter();
        ExportOrchestrator sut = CreateOrchestrator(writer);

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — no re-execution, no throw
        job.Status.ShouldBe(ExportJobStatus.Exporting);
        await writer.DidNotReceive().WriteAsync(
            Arg.Any<Stream>(), Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<object?[]>>(), Arg.Any<CancellationToken>());
        await _jobWriter.DidNotReceive().UpdateAsync(Arg.Any<ExportJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_failure_when_job_already_terminal_does_not_throw_masking_exception()
    {
        // Arrange — simulates a concurrent delivery racing this one to a terminal state (Failed)
        // moments before the pipeline throws on this instance. Without the catch-block guard,
        // job.Fail() would be invoked a second time from a terminal state, throwing an
        // InvalidOperationException that masks the original IOException.
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter writer = Substitute.For<IExportWriter>();
        writer.CanWrite("csv").Returns(true);
        writer.FileExtension.Returns(".csv");
        writer.MimeType.Returns("text/csv");
        writer.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<object?[]>>(),
            Arg.Any<CancellationToken>())
            .Returns<Task<long>>(_ =>
            {
                job.Fail("Concurrent delivery already failed this job", DateTimeOffset.UtcNow);
                throw new IOException("Disk full");
            });

        ExportOrchestrator sut = CreateOrchestrator(writer);

        // Act & Assert — the original exception propagates unmasked
        IOException ex = await Should.ThrowAsync<IOException>(
            () => sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("Disk full");
        job.Status.ShouldBe(ExportJobStatus.Failed);
        job.ErrorMessage.ShouldBe("Concurrent delivery already failed this job"); // not overwritten by a second Fail() call
    }

    [Fact]
    public async Task ExecuteAsync_OperationCanceledException_propagates_without_catch()
    {
        // Arrange — OperationCanceledException should NOT be caught by the error handler
        var jobId = Guid.NewGuid();
        ExportJob job = BuildJob(jobId, ExportJobStatus.Queued);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter cancelWriter = CreateFailingWriter(new OperationCanceledException());
        ExportOrchestrator sut = CreateOrchestrator(cancelWriter);

        // Act & Assert — should propagate, NOT set job to Failed
        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken));

        job.Status.ShouldNotBe(ExportJobStatus.Failed);
    }

    [Fact]
    public async Task GetDownloadAsync_completed_job_xlsx_returns_correct_mime_type()
    {
        // Arrange — xlsx format
        var jobId = Guid.NewGuid();
        var job = ExportJob.Create(jobId, "Test.Export", "xlsx", new ExportRequest("Test.Export", "xlsx", null, false, null, null, null, null));
        job.MarkAsExporting();
        job.Complete("blob-ref-xlsx", "test_export.xlsx", 0, DateTimeOffset.UtcNow);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IExportWriter xlsxWriter = Substitute.For<IExportWriter>();
        xlsxWriter.CanWrite("xlsx").Returns(true);
        xlsxWriter.MimeType.Returns("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        MemoryStream blobStream = new([1, 2, 3]);
        _fileProvider.OpenAsync("blob-ref-xlsx", Arg.Any<CancellationToken>())
            .Returns(blobStream);

        ExportOrchestrator sutXlsx = CreateOrchestrator(xlsxWriter);

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
            new ExportRequest("Test.Export", "csv", null, false, null, null, null, null));
        job.MarkAsExporting();
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

    // ── Complex field capability tests ──────────────────────────────────

    [Fact]
    public async Task ExportAsync_complex_field_with_tabular_writer_Throw_policy_throws()
    {
        // Arrange — definition with ComplexField, writer is tabular (SupportsHierarchy = false)
        IExportWriter tabularWriter = CreateTabularWriter();
        ExportOrchestrator sut = CreateOrchestrator(tabularWriter, new ComplexExportDefinition());

        ExportRequest request = new("Test.Complex", "csv", null, false, null, null, null, null);

        // Act & Assert
        await Should.ThrowAsync<ExportProviderIncompatibleException>(
            () => sut.ExportAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExportAsync_complex_field_with_tabular_writer_Skip_policy_creates_job()
    {
        // Arrange — Skip policy: ExportAsync succeeds, job queued
        IExportWriter tabularWriter = CreateTabularWriter();
        ExportOrchestrator sut = CreateOrchestrator(tabularWriter, new SkipComplexExportDefinition());

        ExportRequest request = new("Test.SkipComplex", "csv", null, false, null, null, null, null);

        // Act — should NOT throw
        ExportJobResult result = await sut.ExportAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ExportJobStatus.Queued);
    }

    [Fact]
    public async Task ExecuteAsync_complex_field_Skip_policy_strips_complex_fields()
    {
        // Arrange — Skip policy: ExecuteAsync strips complex fields from the output
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.SkipComplex", "csv", null, false, null, null, null, null);
        var job = ExportJob.Create(jobId, "Test.SkipComplex", "csv", request);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IReadOnlyList<ExportFieldDescriptor>? capturedFields = null;
        IExportWriter captureWriter = CreateTabularWriter(captureFields: f => capturedFields = f);
        ExportOrchestrator sut = CreateOrchestrator(captureWriter, new SkipComplexExportDefinition());

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — complex field stripped; only scalar fields remain
        capturedFields.ShouldNotBeNull();
        capturedFields!.ShouldAllBe(f => !f.RequiresHierarchy);
        capturedFields.Any(f => f.PropertyPath == "Name").ShouldBeTrue();
        capturedFields.Any(f => f.PropertyPath == "Tags").ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_complex_field_with_structured_writer_serializes_value()
    {
        // Arrange — structured writer (SupportsHierarchy = true): complex field passes through
        var jobId = Guid.NewGuid();
        ExportRequest request = new("Test.Complex", "json", null, false, null, null, null, null);
        var job = ExportJob.Create(jobId, "Test.Complex", "json", request);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IReadOnlyList<ExportFieldDescriptor>? capturedFields = null;
        List<object?[]> capturedRows = [];
        IExportWriter structuredWriter = CreateStructuredWriter(
            captureFields: f => capturedFields = f,
            captureRows: capturedRows);
        ExportOrchestrator sut = CreateOrchestrator(structuredWriter, new ComplexExportDefinition());

        // Act
        await sut.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — complex field "Tags" kept, its selector value present in every row
        capturedFields.ShouldNotBeNull();
        int tagsIndex = IndexOfField(capturedFields!, "Tags");
        tagsIndex.ShouldBeGreaterThanOrEqualTo(0);
        capturedRows.Count.ShouldBe(2);
        capturedRows.ShouldAllBe(r => r.Length == capturedFields!.Count);
        capturedRows[0][tagsIndex].ShouldBeAssignableTo<List<string>>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static int IndexOfField(IReadOnlyList<ExportFieldDescriptor> fields, string propertyPath)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i].PropertyPath == propertyPath)
            {
                return i;
            }
        }

        return -1;
    }

    private ExportOrchestrator CreateOrchestrator(
        IExportWriter? writerOverride = null,
        ExportDefinition<TestEntity>? definition = null,
        IQueryEngine<TestEntity>? queryEngine = null)
    {
        definition ??= new TestExportDefinition();

        ServiceCollection services = new();
        services.AddSingleton<IExportDataSource<TestEntity>>(new TestDataSource());
        services.AddSingleton(Options.Create(new ExportOptions()));
        services.AddSingleton<IExtraExportFieldProvider>(new NullExtraExportFieldProvider());
        services.AddSingleton<IExportExtraValueResolver>(new NullExportExtraValueResolver());
        if (queryEngine is not null)
        {
            services.AddSingleton(queryEngine);
        }

        ExportPipelineRegistry registry = new(
            [new ExportEntityBinding<TestEntity>(definition)],
            [],
            new NullExtraExportFieldProvider());

        IExportWriter writer = writerOverride ?? CreateCsvWriter();
        ServiceProvider sp = services.BuildServiceProvider();

        return new ExportOrchestrator(
            sp,
            [writer],
            _jobReader,
            _jobWriter,
            _commandSender,
            _fileProvider,
            _clock,
            new SimpleGuidGenerator(),
            _currentTenant,
            registry,
            new NullExtraExportFieldProvider(),
            _metrics,
            NullLogger<ExportOrchestrator>.Instance);
    }

    /// <summary>
    /// Configures a substitute writer to consume the row stream (counting rows for the returned
    /// total), optionally capturing fields and rows.
    /// </summary>
    private static void ConfigureWriterConsumption(
        IExportWriter writer,
        Action<IReadOnlyList<ExportFieldDescriptor>>? captureFields = null,
        List<object?[]>? captureRows = null) =>
        writer.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<object?[]>>(),
            Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                captureFields?.Invoke(call.Arg<IReadOnlyList<ExportFieldDescriptor>>());
                long count = 0;
                await foreach (object?[] row in call.Arg<IAsyncEnumerable<object?[]>>())
                {
                    captureRows?.Add(row);
                    count++;
                }

                return count;
            });

    private static IExportWriter CreateCsvWriter(
        Action<IReadOnlyList<ExportFieldDescriptor>>? captureFields = null,
        List<object?[]>? captureRows = null)
    {
        IExportWriter writer = Substitute.For<IExportWriter>();
        writer.CanWrite("csv").Returns(true);
        writer.FileExtension.Returns(".csv");
        writer.MimeType.Returns("text/csv");
        ConfigureWriterConsumption(writer, captureFields, captureRows);
        return writer;
    }

    private static IExportWriter CreateTabularWriter(
        Action<IReadOnlyList<ExportFieldDescriptor>>? captureFields = null)
    {
        IExportWriter writer = Substitute.For<IExportWriter>();
        writer.CanWrite(Arg.Any<string>()).Returns(true);
        writer.FileExtension.Returns(".csv");
        writer.MimeType.Returns("text/csv");
        writer.Capabilities.Returns(ExportFormatCapabilities.TabularOnly);
        ConfigureWriterConsumption(writer, captureFields);
        return writer;
    }

    private static IExportWriter CreateStructuredWriter(
        Action<IReadOnlyList<ExportFieldDescriptor>>? captureFields = null,
        List<object?[]>? captureRows = null)
    {
        IExportWriter writer = Substitute.For<IExportWriter>();
        writer.CanWrite(Arg.Any<string>()).Returns(true);
        writer.FileExtension.Returns(".json");
        writer.MimeType.Returns("application/json");
        writer.Capabilities.Returns(ExportFormatCapabilities.Structured);
        ConfigureWriterConsumption(writer, captureFields, captureRows);
        return writer;
    }

    private static IExportWriter CreateFailingWriter(Exception exception)
    {
        IExportWriter writer = Substitute.For<IExportWriter>();
        writer.CanWrite("csv").Returns(true);
        writer.FileExtension.Returns(".csv");
        writer.MimeType.Returns("text/csv");
        writer.WriteAsync(
            Arg.Any<Stream>(),
            Arg.Any<IReadOnlyList<ExportFieldDescriptor>>(),
            Arg.Any<IAsyncEnumerable<object?[]>>(),
            Arg.Any<CancellationToken>())
            .Returns<Task<long>>(_ => throw exception);
        return writer;
    }

    private static ExportJob BuildJob(Guid id, ExportJobStatus status)
    {
        var job = ExportJob.Create(
            id,
            "Test.Export",
            "csv",
            new ExportRequest("Test.Export", "csv", null, false, null, null, null, null));

        // Transition to desired status using behavior methods
        if (status == ExportJobStatus.Exporting)
        {
            job.MarkAsExporting();
        }
        else if (status == ExportJobStatus.Completed)
        {
            job.MarkAsExporting();
            job.Complete("blob-ref", "export.csv", 0, DateTimeOffset.UtcNow);
        }
        else if (status == ExportJobStatus.Failed)
        {
            job.MarkAsExporting();
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
        public List<string> Tags { get; set; } = [];
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

        public Task<PagedResult<TProjection>> ExecuteAsync<TProjection>(
            IQueryable<TestEntity> source, QueryRequest request,
            System.Linq.Expressions.Expression<Func<TestEntity, TProjection>> projection,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GroupedResult<TestEntity>> ExecuteGroupedAsync(
            IQueryable<TestEntity> source, QueryRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GroupedResult<TProjection>> ExecuteGroupedAsync<TProjection>(
            IQueryable<TestEntity> source, QueryRequest request,
            System.Linq.Expressions.Expression<Func<TestEntity, TProjection>> projection,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public QueryMetadata GetMetadata() =>
            throw new NotSupportedException();

        public IQueryable<TestEntity> BuildFilteredQuery(IQueryable<TestEntity> source, QueryRequest request) =>
            throw new NotSupportedException();
    }

    private sealed class ComplexExportDefinition : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.Complex";

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder) =>
            builder
                .Field(e => e.Name)
                .ComplexField("Tags", e => e.Tags);
    }

    private sealed class SkipComplexExportDefinition : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.SkipComplex";
        public override OnIncompatibleFieldPolicy OnIncompatibleField => OnIncompatibleFieldPolicy.Skip;

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder) =>
            builder
                .Field(e => e.Name)
                .ComplexField("Tags", e => e.Tags);
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
                    Tags = ["dotnet", "export"],
                },
                new()
                {
                    Name = "Jane",
                    Email = "jane@test.com",
                    Company = null,
                    Tags = [],
                },
            }.AsQueryable();
    }
}
