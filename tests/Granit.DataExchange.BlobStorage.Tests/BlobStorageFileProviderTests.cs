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

        await using var content = new MemoryStream([4, 5, 6]);
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

    [Fact]
    public async Task StreamingSaveAsync_WritesThroughMultipartStreamAndCompletes()
    {
        var blobId = Guid.NewGuid();
        _guidGenerator.Create().Returns(blobId);
        _keyStrategy.BuildObjectKey("test-container", blobId).Returns("tenant/test-container/2026/04/blob-id");

        FakeMultipartWriteStream multipartStream = new();
        _storeProvider.OpenWriteMultipartAsync("test-bucket", "tenant/test-container/2026/04/blob-id", "text/csv", Arg.Any<CancellationToken>())
            .Returns(multipartStream);

        byte[] payload = [1, 2, 3];
        string reference = await _sut.SaveAsync(
            "report.csv",
            "text/csv",
            async (stream, ct) => await stream.WriteAsync(payload, ct),
            TestContext.Current.CancellationToken);

        reference.ShouldBe("tenant/test-container/2026/04/blob-id");
        multipartStream.WrittenBytes.ShouldBe(payload);
        multipartStream.Completed.ShouldBeTrue();
        multipartStream.Aborted.ShouldBeFalse();
        multipartStream.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task StreamingSaveAsync_PassesContentTypeThrough()
    {
        _guidGenerator.Create().Returns(Guid.NewGuid());
        _keyStrategy.BuildObjectKey(Arg.Any<string>(), Arg.Any<Guid>()).Returns("some/key");

        FakeMultipartWriteStream multipartStream = new();
        _storeProvider.OpenWriteMultipartAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(multipartStream);

        await _sut.SaveAsync(
            "report.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            (_, _) => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        await _storeProvider.Received(1).OpenWriteMultipartAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamingSaveAsync_WriterThrows_AbortsMultipartStream()
    {
        _guidGenerator.Create().Returns(Guid.NewGuid());
        _keyStrategy.BuildObjectKey(Arg.Any<string>(), Arg.Any<Guid>()).Returns("some/key");

        FakeMultipartWriteStream multipartStream = new();
        _storeProvider.OpenWriteMultipartAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(multipartStream);

        var writerException = new IOException("Disk full");

        await Should.ThrowAsync<IOException>(() => _sut.SaveAsync(
            "report.csv",
            "text/csv",
            (_, _) => throw writerException,
            TestContext.Current.CancellationToken));

        multipartStream.Completed.ShouldBeFalse();
        multipartStream.Aborted.ShouldBeTrue();
        multipartStream.Disposed.ShouldBeTrue();
    }

    /// <summary>
    /// Minimal <see cref="MultipartWriteStream"/> fake capturing written bytes and lifecycle calls,
    /// standing in for the provider-native stream (S3/Azure) the real code path opens.
    /// </summary>
    private sealed class FakeMultipartWriteStream : MultipartWriteStream
    {
        private readonly MemoryStream _buffer = new();

        public bool Completed { get; private set; }

        public bool Aborted { get; private set; }

        public bool Disposed { get; private set; }

        public byte[] WrittenBytes => _buffer.ToArray();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _buffer.Length;

        public override long Position
        {
            get => _buffer.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _buffer.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            _buffer.Write(buffer, offset, count);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _buffer.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            _buffer.WriteAsync(buffer, cancellationToken);

        public override Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            Completed = true;
            return Task.CompletedTask;
        }

        public override Task AbortAsync(CancellationToken cancellationToken = default)
        {
            Aborted = true;
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
