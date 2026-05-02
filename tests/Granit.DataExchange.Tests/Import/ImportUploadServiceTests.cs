using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Pipeline;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import;

public sealed class ImportUploadServiceTests
{
    private readonly IDataExchangeFileProvider _fileProvider = Substitute.For<IDataExchangeFileProvider>();
    private readonly IImportJobWriter _jobWriter = Substitute.For<IImportJobWriter>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IImportDefinitionDescriptor _descriptor = Substitute.For<IImportDefinitionDescriptor>();
    private readonly ImportUploadService _sut;

    public ImportUploadServiceTests()
    {
        _descriptor.Name.Returns("Patients");
        _descriptor.EntityType.Returns(typeof(object));
        _descriptor.MaxFileSizeMb.Returns(10);
        _descriptor.AllowedMimeTypes.Returns(new[] { "text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });

        _guidGenerator.Create().Returns(Guid.NewGuid());
        _clock.Now.Returns(new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero));
        _fileProvider.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Granit.Domain.ValueObjects.BlobReference.Create("blob://test/file.csv"));

        var services = new ServiceCollection();
        services.AddSingleton<IImportDefinitionDescriptor>(_descriptor);
        ServiceProvider sp = services.BuildServiceProvider();

        _sut = new ImportUploadService(
            sp,
            _fileProvider,
            _jobWriter,
            _guidGenerator,
            _clock,
            NullLogger<ImportUploadService>.Instance);
    }

    [Fact]
    public async Task UploadAsync_WithValidFile_ReturnsSuccess()
    {
        // Arrange
        using var stream = new MemoryStream("Name,Email\nAlice,alice@test.com"u8.ToArray());

        // Act
        ImportUploadResult result = await _sut.UploadAsync(
            "patients.csv", "text/csv", 100, stream, "Patients",
            TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Job.ShouldNotBeNull();
        result.Job!.DefinitionName.ShouldBe("Patients");
        result.Job.MimeType.ShouldBe("text/csv");
        result.Job.Status.ShouldBe(ImportJobStatus.Created);
        await _fileProvider.Received(1).SaveAsync("patients.csv", stream, Arg.Any<CancellationToken>());
        await _jobWriter.Received(1).CreateAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_UnknownDefinition_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        ImportUploadResult result = await _sut.UploadAsync(
            "file.csv", "text/csv", 3, stream, "NonExistent",
            TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.ErrorDetail!.ShouldContain("Unknown import definition");
        await _fileProvider.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UploadAsync_EmptyFile_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        ImportUploadResult result = await _sut.UploadAsync(
            "empty.csv", "text/csv", 0, stream, "Patients",
            TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.ErrorDetail.ShouldBe("File is empty.");
    }

    [Fact]
    public async Task UploadAsync_FileTooLarge_ReturnsFailure()
    {
        // Arrange
        long size = 11 * 1024 * 1024; // 11 MB, limit is 10
        using var stream = new MemoryStream([1]);

        // Act
        ImportUploadResult result = await _sut.UploadAsync(
            "big.csv", "text/csv", size, stream, "Patients",
            TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.ErrorDetail!.ShouldContain("maximum allowed size of 10 MB");
    }

    [Fact]
    public async Task UploadAsync_ExactlyAtMaxSize_Succeeds()
    {
        // Arrange — exactly 10 MB
        long size = 10 * 1024 * 1024;
        using var stream = new MemoryStream([1]);

        // Act
        ImportUploadResult result = await _sut.UploadAsync(
            "exact.csv", "text/csv", size, stream, "Patients",
            TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task UploadAsync_DisallowedMimeType_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        ImportUploadResult result = await _sut.UploadAsync(
            "data.json", "application/json", 3, stream, "Patients",
            TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.ErrorDetail!.ShouldContain("MIME type 'application/json' is not allowed");
        result.ErrorDetail!.ShouldContain("text/csv");
    }

    [Fact]
    public async Task UploadAsync_StripsPathFromFileName()
    {
        // Arrange — simulate a path traversal attempt
        using var stream = new MemoryStream("data"u8.ToArray());

        // Act
        ImportUploadResult result = await _sut.UploadAsync(
            "../../../etc/passwd", "text/csv", 4, stream, "Patients",
            TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Job!.OriginalFileName.ShouldBe("passwd");
        await _fileProvider.Received(1).SaveAsync("passwd", stream, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_SetsCreatedAtFromClock()
    {
        // Arrange
        var expectedTime = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);
        _clock.Now.Returns(expectedTime);
        using var stream = new MemoryStream("data"u8.ToArray());

        // Act
        ImportUploadResult result = await _sut.UploadAsync(
            "file.csv", "text/csv", 4, stream, "Patients",
            TestContext.Current.CancellationToken);

        // Assert
        result.Job!.CreatedAt.ShouldBe(expectedTime);
    }

    [Fact]
    public async Task UploadAsync_CaseInsensitiveDefinitionName()
    {
        // Arrange
        using var stream = new MemoryStream("data"u8.ToArray());

        // Act
        ImportUploadResult result = await _sut.UploadAsync(
            "file.csv", "text/csv", 4, stream, "patients", // lowercase
            TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }
}
