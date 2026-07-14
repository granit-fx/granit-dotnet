using System.Diagnostics.Metrics;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Messages;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Granit.Domain.ValueObjects;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import;

public sealed class ImportOrchestratorTests
{
    private readonly IImportJobReader _jobReader = Substitute.For<IImportJobReader>();
    private readonly IImportJobWriter _jobWriter = Substitute.For<IImportJobWriter>();
    private readonly IDataExchangeFileProvider _fileProvider = Substitute.For<IDataExchangeFileProvider>();
    private readonly IImportPipelineRegistry _registry = Substitute.For<IImportPipelineRegistry>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly DataExchangeMetrics _metrics;
    private readonly ImportOptions _optionsValue = new();
    private readonly ImportOrchestrator _orchestrator;

    public ImportOrchestratorTests()
    {
        _metrics = new DataExchangeMetrics(new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>());
        _registry.GetAll().Returns([]);

        ServiceCollection services = new();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        _orchestrator = new ImportOrchestrator(
            _jobReader, _jobWriter, _fileProvider, _registry, serviceProvider,
            _clock, _metrics, Options.Create(_optionsValue), NullLogger<ImportOrchestrator>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobNotFound_ThrowsInvalidOperationException()
    {
        _jobReader.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ImportJob?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _orchestrator.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task DryRunAsync_WhenJobNotFound_ThrowsInvalidOperationException()
    {
        _jobReader.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ImportJob?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _orchestrator.DryRunAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDefinitionNotRegistered_FailsReportListingRegisteredNames()
    {
        // Arrange — a mapped job whose definition is missing from the registry
        var jobId = Guid.NewGuid();
        ImportJob job = BuildMappedJob(jobId, "Missing.Definition");
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IImportPipelineDescriptor other = Substitute.For<IImportPipelineDescriptor>();
        other.DefinitionName.Returns("Other.Definition");
        _registry.Find("Missing.Definition").Returns((IImportPipelineDescriptor?)null);
        _registry.GetAll().Returns([other]);

        // Act — the orchestrator folds the failure into the report (and logs it)
        ImportReport report = await _orchestrator.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        report.FinalStatus.ShouldBe(ImportJobStatus.Failed);
        report.RowErrors.Count.ShouldBe(1);
        report.RowErrors[0].Kind.ShouldBe(ImportRowErrorKind.Persistence);
        report.RowErrors[0].Message.ShouldContain("Missing.Definition");
        report.RowErrors[0].Message.ShouldContain("Other.Definition");
        job.Status.ShouldBe(ImportJobStatus.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPipelineSucceeds_CompletesJobAndBuffersEventOnAggregate()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildMappedJob(jobId, "Test.Import");
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);
        _fileProvider.OpenAsync(job.BlobReference, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream([1])));

        ImportReport report = new()
        {
            TotalRows = 3,
            SucceededRows = 2,
            FailedRows = 1,
            SkippedRows = 0,
            InsertedRows = 2,
            UpdatedRows = 0,
            Duration = TimeSpan.FromSeconds(1),
            FinalStatus = ImportJobStatus.PartiallyCompleted,
            RowErrors = [],
        };

        IImportPipeline pipeline = Substitute.For<IImportPipeline>();
        pipeline.ExecuteAsync(Arg.Any<ImportPipelineContext>(), Arg.Any<CancellationToken>()).Returns(report);
        IImportPipelineDescriptor descriptor = Substitute.For<IImportPipelineDescriptor>();
        descriptor.Create(Arg.Any<IServiceProvider>()).Returns(pipeline);
        _registry.Find("Test.Import").Returns(descriptor);

        // Act
        ImportReport result = await _orchestrator.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — Complete() buffers the Eto on the aggregate itself (transactional outbox);
        // no event bus is involved — DomainEventDispatcherInterceptor drains IntegrationEvents
        // pre-commit on the next SaveChanges.
        result.ShouldBeSameAs(report);
        job.Status.ShouldBe(ImportJobStatus.PartiallyCompleted);
        ImportJobCompletedEto eto = job.IntegrationEvents.OfType<ImportJobCompletedEto>().ShouldHaveSingleItem();
        eto.ImportJobId.ShouldBe(jobId);
        eto.Status.ShouldBe(ImportJobStatus.PartiallyCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_redelivered_terminal_job_returns_existing_report_without_reexecuting()
    {
        // Arrange — simulates Wolverine at-least-once redelivery of a command whose job already
        // reached a terminal state on a prior delivery.
        var jobId = Guid.NewGuid();
        ImportJob job = BuildCompletedJob(jobId, "Test.Import", out ImportReport originalReport);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IImportPipeline pipeline = Substitute.For<IImportPipeline>();
        IImportPipelineDescriptor descriptor = Substitute.For<IImportPipelineDescriptor>();
        descriptor.Create(Arg.Any<IServiceProvider>()).Returns(pipeline);
        _registry.Find("Test.Import").Returns(descriptor);

        // Act
        ImportReport result = await _orchestrator.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — no re-execution, no throw, existing report returned
        result.ShouldBeSameAs(originalReport);
        job.Status.ShouldBe(ImportJobStatus.Completed);
        await pipeline.DidNotReceive().ExecuteAsync(Arg.Any<ImportPipelineContext>(), Arg.Any<CancellationToken>());
        await _jobWriter.DidNotReceive().UpdateAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_job_already_executing_returns_without_reexecuting()
    {
        // Arrange — another (or an earlier in-flight) delivery already owns this run.
        var jobId = Guid.NewGuid();
        ImportJob job = BuildMappedJob(jobId, "Test.Import");
        job.MarkAsExecuting();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        IImportPipeline pipeline = Substitute.For<IImportPipeline>();
        IImportPipelineDescriptor descriptor = Substitute.For<IImportPipelineDescriptor>();
        descriptor.Create(Arg.Any<IServiceProvider>()).Returns(pipeline);
        _registry.Find("Test.Import").Returns(descriptor);

        // Act
        await _orchestrator.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — no re-execution, no throw
        job.Status.ShouldBe(ImportJobStatus.Executing);
        await pipeline.DidNotReceive().ExecuteAsync(Arg.Any<ImportPipelineContext>(), Arg.Any<CancellationToken>());
        await _jobWriter.DidNotReceive().UpdateAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DeleteUploadedFileOnSuccess_enabled_and_zero_failures_deletes_file()
    {
        // Arrange
        _optionsValue.DeleteUploadedFileOnSuccess = true;
        var jobId = Guid.NewGuid();
        ImportJob job = BuildMappedJob(jobId, "Test.Import");
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);
        _fileProvider.OpenAsync(job.BlobReference, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream([1])));

        ImportReport report = SuccessReport(failedRows: 0);
        IImportPipeline pipeline = Substitute.For<IImportPipeline>();
        pipeline.ExecuteAsync(Arg.Any<ImportPipelineContext>(), Arg.Any<CancellationToken>()).Returns(report);
        IImportPipelineDescriptor descriptor = Substitute.For<IImportPipelineDescriptor>();
        descriptor.Create(Arg.Any<IServiceProvider>()).Returns(pipeline);
        _registry.Find("Test.Import").Returns(descriptor);

        // Act
        await _orchestrator.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        await _fileProvider.Received(1).DeleteAsync(job.BlobReference, Arg.Any<CancellationToken>());
        job.FileDeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DeleteUploadedFileOnSuccess_enabled_with_failed_rows_keeps_file()
    {
        // Arrange
        _optionsValue.DeleteUploadedFileOnSuccess = true;
        var jobId = Guid.NewGuid();
        ImportJob job = BuildMappedJob(jobId, "Test.Import");
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);
        _fileProvider.OpenAsync(job.BlobReference, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream([1])));

        ImportReport report = SuccessReport(failedRows: 1);
        IImportPipeline pipeline = Substitute.For<IImportPipeline>();
        pipeline.ExecuteAsync(Arg.Any<ImportPipelineContext>(), Arg.Any<CancellationToken>()).Returns(report);
        IImportPipelineDescriptor descriptor = Substitute.For<IImportPipelineDescriptor>();
        descriptor.Create(Arg.Any<IServiceProvider>()).Returns(pipeline);
        _registry.Find("Test.Import").Returns(descriptor);

        // Act
        await _orchestrator.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — failed rows remain: the correction-file endpoint still needs the source file
        await _fileProvider.DidNotReceive().DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
        job.FileDeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DeleteUploadedFileOnSuccess_disabled_keeps_file()
    {
        // Arrange — option off (default)
        var jobId = Guid.NewGuid();
        ImportJob job = BuildMappedJob(jobId, "Test.Import");
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);
        _fileProvider.OpenAsync(job.BlobReference, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream([1])));

        ImportReport report = SuccessReport(failedRows: 0);
        IImportPipeline pipeline = Substitute.For<IImportPipeline>();
        pipeline.ExecuteAsync(Arg.Any<ImportPipelineContext>(), Arg.Any<CancellationToken>()).Returns(report);
        IImportPipelineDescriptor descriptor = Substitute.For<IImportPipelineDescriptor>();
        descriptor.Create(Arg.Any<IServiceProvider>()).Returns(pipeline);
        _registry.Find("Test.Import").Returns(descriptor);

        // Act
        await _orchestrator.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        await _fileProvider.DidNotReceive().DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
        job.FileDeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DeleteUploadedFileOnSuccess_deleteFailure_stillReportsSuccessAndLogsWarning()
    {
        // Arrange
        _optionsValue.DeleteUploadedFileOnSuccess = true;
        var jobId = Guid.NewGuid();
        ImportJob job = BuildMappedJob(jobId, "Test.Import");
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);
        _fileProvider.OpenAsync(job.BlobReference, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream([1])));
        _fileProvider.DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Blob storage unavailable"));

        ImportReport report = SuccessReport(failedRows: 0);
        IImportPipeline pipeline = Substitute.For<IImportPipeline>();
        pipeline.ExecuteAsync(Arg.Any<ImportPipelineContext>(), Arg.Any<CancellationToken>()).Returns(report);
        IImportPipelineDescriptor descriptor = Substitute.For<IImportPipelineDescriptor>();
        descriptor.Create(Arg.Any<IServiceProvider>()).Returns(pipeline);
        _registry.Find("Test.Import").Returns(descriptor);

        // Act — the deletion failure must not fail the import
        ImportReport result = await _orchestrator.ExecuteAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSameAs(report);
        job.Status.ShouldBe(ImportJobStatus.Completed);
        job.FileDeletedAt.ShouldBeNull();
    }

    private static ImportReport SuccessReport(int failedRows) => new()
    {
        TotalRows = 10,
        SucceededRows = 10 - failedRows,
        FailedRows = failedRows,
        SkippedRows = 0,
        InsertedRows = 10 - failedRows,
        UpdatedRows = 0,
        Duration = TimeSpan.FromSeconds(1),
        FinalStatus = failedRows == 0 ? ImportJobStatus.Completed : ImportJobStatus.PartiallyCompleted,
        RowErrors = [],
    };

    private static ImportJob BuildMappedJob(Guid id, string definitionName)
    {
        var job = ImportJob.Create(id, definitionName, "TestEntity", "file.csv", "text/csv", 10, "blob-1");
        job.CreatedAt = DateTimeOffset.UtcNow;
        job.MarkAsPreviewed();
        job.ConfirmMappings([new ImportColumnMapping("Name", "Name", MappingConfidence.Exact)]);
        return job;
    }

    private static ImportJob BuildCompletedJob(Guid id, string definitionName, out ImportReport report)
    {
        ImportJob job = BuildMappedJob(id, definitionName);
        job.MarkAsExecuting();
        report = SuccessReport(failedRows: 0);
        job.Complete(ImportJobStatus.Completed, report, DateTimeOffset.UtcNow);
        return job;
    }
}
