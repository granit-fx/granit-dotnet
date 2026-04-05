using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import;

public sealed class ImportPreviewServiceTests
{
    private readonly IImportJobReader _jobReader = Substitute.For<IImportJobReader>();
    private readonly IImportJobWriter _jobWriter = Substitute.For<IImportJobWriter>();
    private readonly IImportFileProvider _fileProvider = Substitute.For<IImportFileProvider>();
    private readonly IMappingSuggestionService _mappingService = Substitute.For<IMappingSuggestionService>();
    private readonly IImportDefinitionDescriptor _descriptor = Substitute.For<IImportDefinitionDescriptor>();
    private readonly IFileParser _parser = Substitute.For<IFileParser>();
    private readonly ImportPreviewService _sut;

    public ImportPreviewServiceTests()
    {
        _descriptor.Name.Returns("Patients");
        _descriptor.EntityType.Returns(typeof(object));
        _descriptor.GetFieldMetadata().Returns([
            new ImportFieldMetadata("Name", "String", "Full Name", null, true),
            new ImportFieldMetadata("Email", "String", "Email address", null, false),
        ]);

        _parser.CanParse("text/csv").Returns(true);
        _parser.ExtractHeadersAsync(Arg.Any<Stream>(), Arg.Any<FileParsingOptions>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>(["Name", "Email"]));
        _parser.ReadPreviewAsync(Arg.Any<Stream>(), Arg.Any<FileParsingOptions>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<string[]>([["Alice", "alice@test.com"], ["Bob", "bob@test.com"]]));

        _fileProvider.OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Name,Email\nAlice,alice@test.com"))));

        var services = new ServiceCollection();
        services.AddSingleton<IImportDefinitionDescriptor>(_descriptor);
        services.AddSingleton<IFileParser>(_parser);
        ServiceProvider sp = services.BuildServiceProvider();

        _sut = new ImportPreviewService(
            _jobReader,
            _jobWriter,
            _fileProvider,
            _mappingService,
            sp);
    }

    [Fact]
    public async Task PreviewAsync_WhenJobExists_ReturnsPreviewAndTransitionsState()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Created);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        ImportPreviewResult? result = await _sut.PreviewAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result!.Headers.Count.ShouldBe(2);
        result.Headers[0].ShouldBe("Name");
        result.Headers[1].ShouldBe("Email");
        result.PreviewRows.Count.ShouldBe(2);
        result.FieldMetadata.Count.ShouldBe(2);

        // Job should be transitioned to Previewed
        job.Status.ShouldBe(ImportJobStatus.Previewed);
        await _jobWriter.Received(1).UpdateAsync(
            Arg.Is<ImportJob>(j => j.Status == ImportJobStatus.Previewed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewAsync_WhenJobNotFound_ReturnsNull()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ImportJob?)null);

        // Act
        ImportPreviewResult? result = await _sut.PreviewAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
        await _jobWriter.DidNotReceiveWithAnyArgs().UpdateAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PreviewAsync_WhenDefinitionNotFound_ReturnsNull()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = ImportJob.Create(jobId, "Unknown", "Object", "file.csv", "text/csv", 100, "blob-1");
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        ImportPreviewResult? result = await _sut.PreviewAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task PreviewAsync_WhenNoParserForMimeType_ReturnsNull()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = ImportJob.Create(jobId, "Patients", "Object", "file.xml", "application/xml", 100, "blob-1");
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        ImportPreviewResult? result = await _sut.PreviewAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task PreviewAsync_OpensFileStreamTwice_ForHeadersAndPreview()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Created);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        await _sut.PreviewAsync(jobId, TestContext.Current.CancellationToken);

        // Assert — two separate OpenAsync calls (headers + preview rows)
        await _fileProvider.Received(2).OpenAsync("blob-ref-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewAsync_IncludesFieldMetadata()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Created);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        ImportPreviewResult? result = await _sut.PreviewAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result!.FieldMetadata.Count.ShouldBe(2);
        result.FieldMetadata[0].PropertyPath.ShouldBe("Name");
        result.FieldMetadata[0].IsRequired.ShouldBeTrue();
        result.FieldMetadata[1].PropertyPath.ShouldBe("Email");
        result.FieldMetadata[1].IsRequired.ShouldBeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ImportJob BuildJob(Guid id, ImportJobStatus status)
    {
        var job = ImportJob.Create(
            id,
            "Patients",
            "Object",
            "patients.csv",
            "text/csv",
            100,
            "blob-ref-1");
        job.CreatedAt = DateTimeOffset.UtcNow;

        if (status == ImportJobStatus.Previewed)
        {
            job.MarkAsPreviewed();
        }

        return job;
    }
}
