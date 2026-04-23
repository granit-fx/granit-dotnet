using System.Diagnostics.Metrics;
using Granit.BlobStorage.Diagnostics;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Exceptions;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests;

public sealed class DefaultBlobStorageTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IBlobDescriptorReader _reader = Substitute.For<IBlobDescriptorReader>();
    private readonly IBlobDescriptorWriter _writer = Substitute.For<IBlobDescriptorWriter>();
    private readonly IBlobKeyStrategy _keyStrategy = Substitute.For<IBlobKeyStrategy>();
    private readonly IBlobStoreProvider _storeProvider = Substitute.For<IBlobStoreProvider>();
    private readonly IPresignedUrlProvider _presignedUrlProvider = Substitute.For<IPresignedUrlProvider>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly BlobStorageMetrics _metrics = new(new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>());
    private readonly DefaultBlobStorage _sut;

    public DefaultBlobStorageTests()
    {
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(TenantId);
        _clock.Now.Returns(Now);

        _sut = new DefaultBlobStorage(
            _reader,
            _writer,
            _keyStrategy,
            _storeProvider,
            _presignedUrlProvider,
            [],
            _guidGenerator,
            _clock,
            _currentTenant,
            _metrics,
            NullLogger<DefaultBlobStorage>.Instance,
            Microsoft.Extensions.Options.Options.Create(new BlobStorageOptions()));
    }

    // ── InitiateUploadAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task InitiateUploadAsync_ShouldSavePendingDescriptorAndReturnTicket()
    {
        // Arrange
        var blobId = Guid.NewGuid();
        _guidGenerator.Create().Returns(blobId);
        _keyStrategy.BuildObjectKey("medical-images", blobId)
            .Returns($"{TenantId}/medical-images/2026/02/{blobId}");
        _keyStrategy.ResolveBucketName("medical-images").Returns("granit-blobs");

        PresignedUploadTicket expectedTicket = new(
            blobId,
            new Uri("https://s3.example.com/presigned"),
            "PUT",
            Now.AddMinutes(15),
            new Dictionary<string, string> { ["Content-Type"] = "image/jpeg" });

        _presignedUrlProvider.GenerateUploadTicketAsync(
            "granit-blobs",
            Arg.Any<string>(),
            blobId,
            Arg.Any<BlobUploadRequest>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()).Returns(expectedTicket);

        BlobUploadRequest request = new("radio.jpg", "image/jpeg", 10_000_000);

        // Act
        PresignedUploadTicket ticket = await _sut.InitiateUploadAsync("medical-images", request, TestContext.Current.CancellationToken);

        // Assert
        ticket.ShouldBe(expectedTicket);
    }

    [Fact]
    public async Task InitiateUploadAsync_ShouldSavePendingDescriptorWithCorrectFields()
    {
        // Arrange
        var blobId = Guid.NewGuid();
        _guidGenerator.Create().Returns(blobId);
        string expectedKey = $"{TenantId}/medical-images/2026/02/{blobId}";
        _keyStrategy.BuildObjectKey("medical-images", blobId).Returns(expectedKey);
        _keyStrategy.ResolveBucketName("medical-images").Returns("granit-blobs");

        PresignedUploadTicket ticket = new(blobId, new Uri("https://s3.example.com/up"), "PUT", Now.AddMinutes(15), new Dictionary<string, string>());
        _presignedUrlProvider.GenerateUploadTicketAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(),
            Arg.Any<BlobUploadRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ticket);

        BlobUploadRequest request = new("ordonnance.pdf", "application/pdf", 5_000_000);

        // Act
        await _sut.InitiateUploadAsync("medical-images", request, TestContext.Current.CancellationToken);

        // Assert
        await _writer.Received(1).SaveAsync(
            Arg.Is<BlobDescriptor>(d =>
                d.Status == BlobStatus.Pending &&
                d.Id == blobId &&
                d.TenantId == TenantId &&
                d.ContainerName == "medical-images" &&
                d.ObjectKey == expectedKey &&
                d.OriginalFileName == "ordonnance.pdf" &&
                d.DeclaredContentType == "application/pdf" &&
                d.CreatedAt == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateUploadAsync_ShouldPassCorrectExpiryToUrlGenerator()
    {
        // Arrange
        var blobId = Guid.NewGuid();
        _guidGenerator.Create().Returns(blobId);
        _keyStrategy.BuildObjectKey(Arg.Any<string>(), Arg.Any<Guid>()).Returns("key");
        _keyStrategy.ResolveBucketName(Arg.Any<string>()).Returns("bucket");
        _presignedUrlProvider.GenerateUploadTicketAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(),
            Arg.Any<BlobUploadRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUploadTicket(blobId, new Uri("https://s3.example.com/up"), "PUT", Now.AddMinutes(15), new Dictionary<string, string>()));

        BlobStorageOptions customOptions = new() { UploadUrlExpiry = TimeSpan.FromMinutes(30) };
        DefaultBlobStorage sutWithCustomOptions = new(
            _reader, _writer, _keyStrategy, _storeProvider, _presignedUrlProvider,
            [],
            _guidGenerator, _clock, _currentTenant,
            _metrics,
            NullLogger<DefaultBlobStorage>.Instance,
            Microsoft.Extensions.Options.Options.Create(customOptions));

        // Act
        await sutWithCustomOptions.InitiateUploadAsync("docs", new BlobUploadRequest("file.pdf", "application/pdf", 1_000_000), TestContext.Current.CancellationToken);

        // Assert
        await _presignedUrlProvider.Received(1).GenerateUploadTicketAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<BlobUploadRequest>(),
            TimeSpan.FromMinutes(30),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateUploadAsync_WhenNoActiveTenant_ShouldSucceedWithNullTenantId()
    {
        // Arrange
        _currentTenant.IsAvailable.Returns(false);
        _currentTenant.Id.Returns((Guid?)null);

        // Act
        Func<Task> act = async () =>
            await _sut.InitiateUploadAsync("docs", new BlobUploadRequest("f.pdf", "application/pdf", 1_000));

        // Assert — single-tenant apps must not be blocked
        await Should.NotThrowAsync(act);
        await _writer.Received(1).SaveAsync(
            Arg.Is<BlobDescriptor>(d => d.TenantId == null),
            Arg.Any<CancellationToken>());
    }

    // ── CreateDownloadUrlAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateDownloadUrlAsync_WhenBlobIsValid_ShouldReturnPresignedUrl()
    {
        // Arrange
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptor = BuildValidDescriptor(blobId);
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns(descriptor);
        _keyStrategy.ResolveBucketName("medical-images").Returns("granit-blobs");

        PresignedDownloadUrl expectedUrl = new(new Uri("https://s3.example.com/download"), Now.AddMinutes(5));
        _presignedUrlProvider.GenerateDownloadUrlAsync(
            "granit-blobs", Arg.Any<string>(), Arg.Any<DownloadUrlOptions?>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(expectedUrl);

        // Act
        PresignedDownloadUrl result = await _sut.CreateDownloadUrlAsync("medical-images", blobId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(expectedUrl);
    }

    [Fact]
    public async Task CreateDownloadUrlAsync_WhenBlobIsUploading_ShouldThrowBlobNotValidException()
    {
        // Arrange
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptor = BuildDescriptorInStatus(blobId, BlobStatus.Uploading);
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns(descriptor);

        // Act
        Func<Task> act = async () => await _sut.CreateDownloadUrlAsync("medical-images", blobId);

        // Assert
        await Should.ThrowAsync<BlobNotValidException>(act);
    }

    [Fact]
    public async Task CreateDownloadUrlAsync_WhenBlobNotFound_ShouldThrowBlobNotFoundException()
    {
        // Arrange
        var blobId = Guid.NewGuid();
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns((BlobDescriptor?)null);

        // Act
        Func<Task> act = async () => await _sut.CreateDownloadUrlAsync("medical-images", blobId);

        // Assert
        await Should.ThrowAsync<BlobNotFoundException>(act);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenBlobIsValid_ShouldDeleteS3ObjectAndTransitionToDeleted()
    {
        // Arrange
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptor = BuildValidDescriptor(blobId);
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns(descriptor);
        _keyStrategy.ResolveBucketName("medical-images").Returns("granit-blobs");

        // Act
        await _sut.DeleteAsync("medical-images", blobId, "GDPR Art. 17", TestContext.Current.CancellationToken);

        // Assert — S3 physically deleted
        await _storeProvider.Received(1).DeleteAsync("granit-blobs", descriptor.ObjectKey, Arg.Any<CancellationToken>());
        // Assert — descriptor updated in store with Deleted status
        await _writer.Received(1).UpdateAsync(
            Arg.Is<BlobDescriptor>(d =>
                d.Status == BlobStatus.Deleted &&
                d.DeletionReason == "GDPR Art. 17" &&
                d.DeletedAt == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenBlobAlreadyDeleted_ShouldBeIdempotentAndNotCallS3()
    {
        // Arrange
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptor = BuildDescriptorInStatus(blobId, BlobStatus.Deleted);
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns(descriptor);

        // Act
        await _sut.DeleteAsync("medical-images", blobId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert — no S3 call, no store update
        await _storeProvider.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _writer.DidNotReceive().UpdateAsync(Arg.Any<BlobDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenBlobNotFound_ShouldThrowBlobNotFoundException()
    {
        // Arrange
        var blobId = Guid.NewGuid();
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns((BlobDescriptor?)null);

        // Act
        Func<Task> act = async () => await _sut.DeleteAsync("medical-images", blobId);

        // Assert
        await Should.ThrowAsync<BlobNotFoundException>(act);
        await _storeProvider.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ShouldPreserveAuditRecord_AfterS3Delete()
    {
        // Arrange — validates the GDPR/ISO 27001 constraint: the DB row must survive deletion
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptor = BuildValidDescriptor(blobId);
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns(descriptor);
        _keyStrategy.ResolveBucketName(Arg.Any<string>()).Returns("granit-blobs");

        // Act
        await _sut.DeleteAsync("medical-images", blobId, "GDPR erasure", TestContext.Current.CancellationToken);

        // Assert — UpdateAsync called (not a delete from DB)
        await _writer.Received(1).UpdateAsync(Arg.Any<BlobDescriptor>(), Arg.Any<CancellationToken>());
        await _writer.DidNotReceive().SaveAsync(Arg.Any<BlobDescriptor>(), Arg.Any<CancellationToken>());
        // No "hard delete" method should exist on IBlobDescriptorStore — there is none by design.
    }

    // ── GetDescriptorAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetDescriptorAsync_WhenBlobExists_ShouldReturnDescriptor()
    {
        // Arrange
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptor = BuildValidDescriptor(blobId);
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns(descriptor);

        // Act
        BlobDescriptor? result = await _sut.GetDescriptorAsync("medical-images", blobId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(descriptor);
    }

    [Fact]
    public async Task GetDescriptorAsync_WhenBlobNotFound_ShouldReturnNull()
    {
        // Arrange
        _reader.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlobDescriptor?)null);

        // Act
        BlobDescriptor? result = await _sut.GetDescriptorAsync("medical-images", Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    // ── ConfirmUploadAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmUploadAsync_WhenAllValidatorsPass_ShouldTransitionToValid()
    {
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptor = BuildDescriptorInStatus(blobId, BlobStatus.Pending);
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns(descriptor);
        _keyStrategy.ResolveBucketName("medical-images").Returns("granit-blobs");
        _storeProvider.GetSizeAsync("granit-blobs", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(512_000L);

        IBlobValidator validator = Substitute.For<IBlobValidator>();
        validator.Order.Returns(10);
        validator.ValidateAsync(Arg.Any<BlobValidationContext>(), Arg.Any<CancellationToken>())
            .Returns(BlobValidationResult.Success("image/jpeg"));

        DefaultBlobStorage sut = BuildSutWithValidators([validator]);
        BlobConfirmationResult result = await sut.ConfirmUploadAsync("medical-images", blobId, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
        result.Status.ShouldBe(BlobStatus.Valid);
        result.VerifiedContentType.ShouldBe("image/jpeg");
        result.SizeBytes.ShouldBe(512_000L);
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenValidatorFails_ShouldRejectAndDeleteStorageObject()
    {
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptor = BuildDescriptorInStatus(blobId, BlobStatus.Pending);
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns(descriptor);
        _keyStrategy.ResolveBucketName("medical-images").Returns("granit-blobs");
        _storeProvider.GetSizeAsync("granit-blobs", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(512_000L);

        IBlobValidator validator = Substitute.For<IBlobValidator>();
        validator.Order.Returns(10);
        validator.ValidateAsync(Arg.Any<BlobValidationContext>(), Arg.Any<CancellationToken>())
            .Returns(BlobValidationResult.Failure("Content-Type mismatch"));

        DefaultBlobStorage sut = BuildSutWithValidators([validator]);
        BlobConfirmationResult result = await sut.ConfirmUploadAsync("medical-images", blobId, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Status.ShouldBe(BlobStatus.Rejected);
        await _storeProvider.Received(1).DeleteAsync("granit-blobs", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenFileNotOnStorage_ShouldReject()
    {
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptor = BuildDescriptorInStatus(blobId, BlobStatus.Pending);
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns(descriptor);
        _keyStrategy.ResolveBucketName("medical-images").Returns("granit-blobs");
        _storeProvider.GetSizeAsync("granit-blobs", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<long>(_ => throw new InvalidOperationException("Not found"));

        BlobConfirmationResult result = await _sut.ConfirmUploadAsync("medical-images", blobId, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.RejectionReason.ShouldBe("File not found in storage provider.");
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenBlobNotPending_ShouldThrow()
    {
        var blobId = Guid.NewGuid();
        _reader.FindAsync(blobId, Arg.Any<CancellationToken>()).Returns(BuildDescriptorInStatus(blobId, BlobStatus.Valid));

        await Should.ThrowAsync<BlobNotValidException>(() => _sut.ConfirmUploadAsync("medical-images", blobId));
    }

    // ── CleanupOrphansAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CleanupOrphansAsync_ShouldRejectOrphans()
    {
        var blobId = Guid.NewGuid();
        BlobDescriptor orphan = BuildDescriptorInStatus(blobId, BlobStatus.Pending);
        _reader.FindOrphanedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([orphan]);
        _keyStrategy.ResolveBucketName("medical-images").Returns("granit-blobs");

        int cleaned = await _sut.CleanupOrphansAsync(TestContext.Current.CancellationToken);

        cleaned.ShouldBe(1);
        await _writer.Received(1).UpdateAsync(Arg.Is<BlobDescriptor>(d => d.Status == BlobStatus.Rejected), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupOrphansAsync_WhenNoOrphans_ShouldReturnZero()
    {
        _reader.FindOrphanedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BlobDescriptor>());

        (await _sut.CleanupOrphansAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private DefaultBlobStorage BuildSutWithValidators(IBlobValidator[] blobValidators) =>
        new(_reader, _writer, _keyStrategy, _storeProvider, _presignedUrlProvider,
            blobValidators, _guidGenerator, _clock, _currentTenant,
            _metrics,
            NullLogger<DefaultBlobStorage>.Instance,
            Microsoft.Extensions.Options.Options.Create(new BlobStorageOptions()));


    private static BlobDescriptor BuildValidDescriptor(Guid blobId)
    {
        var descriptor = BlobDescriptor.Create(
            id: blobId,
            tenantId: TenantId,
            containerName: "medical-images",
            objectKey: $"{TenantId}/medical-images/2026/02/{blobId}",
            request: new BlobUploadRequest("radio.jpg", "image/jpeg", 10_000_000L),
            createdAt: Now);
        descriptor.MarkAsUploading();
        descriptor.MarkAsValid("image/jpeg", 512_000, Now.AddSeconds(5));
        return descriptor;
    }

    private static BlobDescriptor BuildDescriptorInStatus(Guid blobId, BlobStatus target)
    {
        var descriptor = BlobDescriptor.Create(
            id: blobId,
            tenantId: TenantId,
            containerName: "medical-images",
            objectKey: $"{TenantId}/medical-images/2026/02/{blobId}",
            request: new BlobUploadRequest("file.jpg", "image/jpeg", 10_000_000L),
            createdAt: Now);

        switch (target)
        {
            case BlobStatus.Uploading:
                descriptor.MarkAsUploading();
                break;
            case BlobStatus.Valid:
                descriptor.MarkAsUploading();
                descriptor.MarkAsValid("image/jpeg", 512_000, Now);
                break;
            case BlobStatus.Rejected:
                descriptor.MarkAsUploading();
                descriptor.MarkAsRejected("test");
                break;
            case BlobStatus.Deleted:
                descriptor.MarkAsUploading();
                descriptor.MarkAsValid("image/jpeg", 512_000, Now);
                descriptor.MarkAsDeleted(Now.AddDays(1));
                break;
        }
        return descriptor;
    }
}
