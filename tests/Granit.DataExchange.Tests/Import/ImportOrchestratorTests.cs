using System.Diagnostics.Metrics;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Granit.Events;
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
    private readonly ILocalEventBus _eventBus = Substitute.For<ILocalEventBus>();
    private readonly DataExchangeMetrics _metrics;
    private readonly IOptions<ImportOptions> _options = Options.Create(new ImportOptions());
    private readonly ImportOrchestrator _orchestrator;

    public ImportOrchestratorTests()
    {
        _metrics = new DataExchangeMetrics(new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>());
        _registry.GetAll().Returns([]);

        ServiceCollection services = new();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        _orchestrator = new ImportOrchestrator(
            _jobReader, _jobWriter, _fileProvider, _registry, serviceProvider,
            _clock, _eventBus, _metrics, _options, NullLogger<ImportOrchestrator>.Instance);
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
    public async Task ExecuteAsync_WhenPipelineSucceeds_CompletesJobAndPublishesEvent()
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

        // Assert
        result.ShouldBeSameAs(report);
        job.Status.ShouldBe(ImportJobStatus.PartiallyCompleted);
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<Granit.DataExchange.Import.Messages.ImportJobCompletedEto>(e =>
                e.ImportJobId == jobId && e.Status == ImportJobStatus.PartiallyCompleted),
            Arg.Any<CancellationToken>());
    }

    private static ImportJob BuildMappedJob(Guid id, string definitionName)
    {
        var job = ImportJob.Create(id, definitionName, "TestEntity", "file.csv", "text/csv", 10, "blob-1");
        job.CreatedAt = DateTimeOffset.UtcNow;
        job.MarkAsPreviewed();
        job.ConfirmMappings([new ImportColumnMapping("Name", "Name", MappingConfidence.Exact)]);
        return job;
    }
}
