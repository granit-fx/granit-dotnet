using Granit.BlobStorage.GoogleCloud.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.GoogleCloud.Tests.Internal;

public sealed class GcsResumableMultipartWriteStreamTests
{
    private const string ContentType = "application/zip";
    private const int ChunkSize = 1 * 1024 * 1024; // 1 MB — multiple of 256 KiB, smallest sane test size.
    private static readonly Uri SessionUri = new("https://storage.googleapis.com/upload/session/abc123");

    [Fact]
    public async Task CompleteAsync_SinglePayloadUnderChunkThreshold_UploadsSingleFinalChunk()
    {
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();

        await using (GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize))
        {
            await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
            await sut.CompleteAsync(TestContext.Current.CancellationToken);
        }

        await ops.Received(1).UploadChunkAsync(
            SessionUri,
            Arg.Any<ReadOnlyMemory<byte>>(),
            offset: 0L,
            totalSize: 3L,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_AcrossMultipleChunks_FlushesMidStream_AndShipsFinalRemainder()
    {
        List<(long offset, long? totalSize, int length)> captured = [];
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();
        ops.UploadChunkAsync(
                Arg.Any<Uri>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<long>(),
                Arg.Any<long?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured.Add((
                    callInfo.Arg<long>(),
                    callInfo.Arg<long?>(),
                    callInfo.Arg<ReadOnlyMemory<byte>>().Length));
                return Task.CompletedTask;
            });

        await using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);
        // First write hits the threshold → flush as chunk 1.
        await sut.WriteAsync(new byte[ChunkSize], TestContext.Current.CancellationToken);

        captured.Count.ShouldBe(1);
        captured[0].offset.ShouldBe(0L);
        captured[0].length.ShouldBe(ChunkSize);
        captured[0].totalSize.ShouldBeNull();

        // Small follow-up rides Complete as the final chunk.
        await sut.WriteAsync(new byte[1024], TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        captured.Count.ShouldBe(2);
        captured[1].offset.ShouldBe((long)ChunkSize);
        captured[1].length.ShouldBe(1024);
        captured[1].totalSize.ShouldBe(ChunkSize + 1024L);
    }

    [Fact]
    public async Task WriteAsync_OverflowingChunkBoundary_SplitsCleanly()
    {
        // A single write of chunkSize + 100 bytes must produce one chunk-sized flush
        // (mid-stream) and leave 100 bytes for the final flush. GCS rejects mid-stream
        // chunks that aren't a multiple of 256 KiB — this is the regression guard.
        List<int> chunkLengths = [];
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();
        ops.UploadChunkAsync(
                Arg.Any<Uri>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<long>(),
                Arg.Any<long?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                chunkLengths.Add(callInfo.Arg<ReadOnlyMemory<byte>>().Length);
                return Task.CompletedTask;
            });

        await using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);
        await sut.WriteAsync(new byte[ChunkSize + 100], TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        chunkLengths.ShouldBe([ChunkSize, 100]);
    }

    [Fact]
    public async Task UploadChunk_ReceivesOffsetsInOrderStartingAtZero()
    {
        List<long> offsets = [];
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();
        ops.UploadChunkAsync(
                Arg.Any<Uri>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<long>(),
                Arg.Any<long?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                offsets.Add(callInfo.Arg<long>());
                return Task.CompletedTask;
            });

        await using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);
        await sut.WriteAsync(new byte[ChunkSize], TestContext.Current.CancellationToken);
        await sut.WriteAsync(new byte[ChunkSize], TestContext.Current.CancellationToken);
        await sut.WriteAsync(new byte[1024], TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        offsets.ShouldBe([0L, ChunkSize, 2L * ChunkSize]);
    }

    [Fact]
    public async Task NonFinalChunks_PassNullTotalSize_FinalChunkCarriesTotal()
    {
        List<long?> totalSizes = [];
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();
        ops.UploadChunkAsync(
                Arg.Any<Uri>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<long>(),
                Arg.Any<long?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                totalSizes.Add(callInfo.Arg<long?>());
                return Task.CompletedTask;
            });

        await using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);
        await sut.WriteAsync(new byte[ChunkSize], TestContext.Current.CancellationToken);
        await sut.WriteAsync(new byte[ChunkSize], TestContext.Current.CancellationToken);
        await sut.WriteAsync(new byte[500], TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        totalSizes.ShouldBe([null, null, 2L * ChunkSize + 500]);
    }

    [Fact]
    public async Task CompleteAsync_NoWrites_UploadsEmptyFinalChunk()
    {
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();

        await using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await ops.Received(1).UploadChunkAsync(
            SessionUri,
            Arg.Is<ReadOnlyMemory<byte>>(m => m.Length == 0),
            offset: 0L,
            totalSize: 0L,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbortAsync_DeletesSession_AndDoesNotUploadAnyChunk()
    {
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();

        await using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await ops.Received(1).AbortSessionAsync(SessionUri, Arg.Any<CancellationToken>());
        await ops.DidNotReceive().UploadChunkAsync(
            Arg.Any<Uri>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<long>(),
            Arg.Any<long?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisposeAsync_WithoutCompleteOrAbort_AutoAborts()
    {
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();

        await using (GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize))
        {
            await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
            // No explicit Complete or Abort.
        }

        await ops.Received(1).AbortSessionAsync(SessionUri, Arg.Any<CancellationToken>());
        await ops.DidNotReceive().UploadChunkAsync(
            Arg.Any<Uri>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<long>(),
            Arg.Any<long?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_IsIdempotent()
    {
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();

        await using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await ops.Received(1).UploadChunkAsync(
            Arg.Any<Uri>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<long>(),
            Arg.Any<long?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_AfterAbort_Throws()
    {
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();

        await using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CompleteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAsync_AfterComplete_Throws()
    {
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();

        await using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.WriteAsync(new byte[] { 4 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAsync_AfterAbort_Throws()
    {
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();

        await using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ObjectDisposedException>(
            async () => await sut.WriteAsync(new byte[] { 4 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void StreamSurface_RejectsReadsAndSeeks()
    {
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();
        using GcsResumableMultipartWriteStream sut = new(ops, SessionUri, ChunkSize);

        sut.CanRead.ShouldBeFalse();
        sut.CanSeek.ShouldBeFalse();
        sut.CanWrite.ShouldBeTrue();

        Should.Throw<NotSupportedException>(() => sut.Read(new byte[4], 0, 4));
        Should.Throw<NotSupportedException>(() => sut.Seek(0, SeekOrigin.Begin));
        Should.Throw<NotSupportedException>(() => sut.SetLength(10));
        Should.Throw<NotSupportedException>(() => sut.Position = 0);
    }

    [Fact]
    public void Ctor_RejectsChunkSizeBelowMinimum()
    {
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new GcsResumableMultipartWriteStream(ops, SessionUri, chunkSizeBytes: 1024));
    }

    [Fact]
    public void Ctor_RejectsChunkSizeNotAlignedTo256KiB()
    {
        // 300 KiB > 256 KiB minimum but isn't a multiple of 256 KiB — GCS rejects
        // mid-stream chunks of that size, so the stream rejects the config up-front.
        IGcsResumableUploadOperations ops = Substitute.For<IGcsResumableUploadOperations>();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new GcsResumableMultipartWriteStream(ops, SessionUri, chunkSizeBytes: 300 * 1024));
    }
}
