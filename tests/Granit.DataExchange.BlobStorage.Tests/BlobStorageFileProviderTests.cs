using Granit.BlobStorage;
using Granit.BlobStorage.Internal;
using Granit.DataExchange.BlobStorage.Internal;
using Granit.DataExchange.BlobStorage.Options;
using Granit.Guids;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.BlobStorage.Tests;

public sealed class BlobStorageFileProviderTests
{
    private readonly IBlobStoreProvider _storeProvider = Substitute.For<IBlobStoreProvider>();
    private readonly IBlobKeyStrategy _keyStrategy = Substitute.For<IBlobKeyStrategy>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();

    private readonly BlobStorageFileProvider _sut;

    public BlobStorageFileProviderTests()
    {
        IOptions<DataExchangeBlobStorageOptions> options = Microsoft.Extensions.Options.Options.Create(new DataExchangeBlobStorageOptions
        {
            ContainerName = "test-container",
            DefaultContentType = "application/octet-stream",
        });

        _keyStrategy.ResolveBucketName("test-container").Returns("test-bucket");

        _sut = new BlobStorageFileProvider(_storeProvider, _keyStrategy, _guidGenerator, options);
    }

    [Fact]
    public async Task OpenAsync_DelegatesToStoreProvider()
    {
        var expectedStream = new MemoryStream([1, 2, 3]);
        _storeProvider.OpenReadAsync("test-bucket", "some/object/key", Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        Stream result = await _sut.OpenAsync("some/object/key", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expectedStream);
        await _storeProvider.Received(1).OpenReadAsync("test-bucket", "some/object/key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_BuildsKeyAndDelegatesToStoreProvider()
    {
        var blobId = Guid.NewGuid();
        _guidGenerator.Create().Returns(blobId);
        _keyStrategy.BuildObjectKey("test-container", blobId).Returns("tenant/test-container/2026/04/blob-id");

        using var content = new MemoryStream([4, 5, 6]);
        string reference = await _sut.SaveAsync("report.csv", content, TestContext.Current.CancellationToken);

        reference.ShouldBe("tenant/test-container/2026/04/blob-id");
        await _storeProvider.Received(1).SaveAsync(
            "test-bucket",
            "tenant/test-container/2026/04/blob-id",
            content,
            "application/octet-stream",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToStoreProvider()
    {
        await _sut.DeleteAsync("some/object/key", TestContext.Current.CancellationToken);

        await _storeProvider.Received(1).DeleteAsync("test-bucket", "some/object/key", Arg.Any<CancellationToken>());
    }
}
