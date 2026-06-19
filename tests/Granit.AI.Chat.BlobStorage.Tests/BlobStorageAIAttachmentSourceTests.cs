using Granit.AI.Chat.Attachments;
using Granit.BlobStorage;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Chat.BlobStorage.Tests;

public sealed class BlobStorageAIAttachmentSourceTests
{
    private readonly IBlobContentReader _contentReader = Substitute.For<IBlobContentReader>();
    private readonly BlobStorageAIAttachmentSource _sut;

    public BlobStorageAIAttachmentSourceTests()
    {
        _sut = new BlobStorageAIAttachmentSource(_contentReader);
    }

    [Fact]
    public async Task GetAsync_ValidBlob_ReturnsAttachmentData()
    {
        var blobId = Guid.NewGuid();
        byte[] bytes = [1, 2, 3];
        _contentReader.ReadAsync(blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobContent(bytes, "application/pdf", "invoice.pdf"));

        AIAttachmentData? result = await _sut.GetAsync(blobId.ToString(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ContentType.ShouldBe("application/pdf");
        result.FileName.ShouldBe("invoice.pdf");
        result.Bytes.ToArray().ShouldBe(bytes);
    }

    [Fact]
    public async Task GetAsync_InvalidGuid_ReturnsNull()
    {
        AIAttachmentData? result = await _sut.GetAsync("not-a-guid", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _contentReader.DidNotReceiveWithAnyArgs()
            .ReadAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAsync_BlobNotFound_ReturnsNull()
    {
        var blobId = Guid.NewGuid();
        _contentReader.ReadAsync(blobId, Arg.Any<CancellationToken>()).Returns((BlobContent?)null);

        AIAttachmentData? result = await _sut.GetAsync(blobId.ToString(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_BlobNotValid_ReturnsNull()
    {
        var blobId = Guid.NewGuid();
        // IBlobContentReader already filters out non-Valid blobs — returns null for them.
        _contentReader.ReadAsync(blobId, Arg.Any<CancellationToken>()).Returns((BlobContent?)null);

        AIAttachmentData? result = await _sut.GetAsync(blobId.ToString(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }
}
