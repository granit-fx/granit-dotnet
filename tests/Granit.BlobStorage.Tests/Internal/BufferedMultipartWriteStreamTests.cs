using Granit.BlobStorage.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests.Internal;

public sealed class BufferedMultipartWriteStreamTests
{
    private const string Bucket = "exports";
    private const string ObjectKey = "tenant-a/shard-001.zip";
    private const string ContentType = "application/zip";

    private static BufferedMultipartWriteStream CreateSut(IBlobStoreProvider provider) =>
        new(provider, Bucket, ObjectKey, ContentType);

    [Fact]
    public async Task CompleteAsync_ShipsAllBufferedBytesToTheProvider()
    {
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();
        byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8];

        CapturedBytes captured = CaptureSavedBytes(provider);

        await using (BufferedMultipartWriteStream sut = CreateSut(provider))
        {
            await sut.WriteAsync(payload, TestContext.Current.CancellationToken);
            await sut.CompleteAsync(TestContext.Current.CancellationToken);
        }

        await provider.Received(1).SaveAsync(
            Bucket, ObjectKey, Arg.Any<Stream>(), ContentType, Arg.Any<CancellationToken>());
        captured.Value.ShouldBe(payload);
    }

    [Fact]
    public async Task CompleteAsync_IsIdempotent()
    {
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();

        await using BufferedMultipartWriteStream sut = CreateSut(provider);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await provider.Received(1).SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbortAsync_DoesNotShipBytes()
    {
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();

        await using BufferedMultipartWriteStream sut = CreateSut(provider);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await provider.DidNotReceive().SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbortAsync_IsIdempotent()
    {
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();

        await using BufferedMultipartWriteStream sut = CreateSut(provider);
        await sut.AbortAsync(TestContext.Current.CancellationToken);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        // No throw. SaveAsync still uncalled.
        await provider.DidNotReceive().SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisposeAsync_WithoutCompleteOrAbort_DoesNotShipBytes()
    {
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();

        await using (BufferedMultipartWriteStream sut = CreateSut(provider))
        {
            await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
            // No explicit Complete or Abort.
        }

        await provider.DidNotReceive().SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_AfterAbort_Throws()
    {
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();

        await using BufferedMultipartWriteStream sut = CreateSut(provider);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CompleteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAsync_AfterComplete_Throws()
    {
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();

        await using BufferedMultipartWriteStream sut = CreateSut(provider);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.WriteAsync(new byte[] { 4 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAsync_AfterAbort_Throws()
    {
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();

        await using BufferedMultipartWriteStream sut = CreateSut(provider);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ObjectDisposedException>(
            async () => await sut.WriteAsync(new byte[] { 4 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void StreamSurface_RejectsReadsAndSeeks()
    {
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();
        using BufferedMultipartWriteStream sut = CreateSut(provider);

        sut.CanRead.ShouldBeFalse();
        sut.CanSeek.ShouldBeFalse();
        sut.CanWrite.ShouldBeTrue();

        Should.Throw<NotSupportedException>(() => sut.Read(new byte[4], 0, 4));
        Should.Throw<NotSupportedException>(() => sut.Seek(0, SeekOrigin.Begin));
        Should.Throw<NotSupportedException>(() => sut.SetLength(10));
        Should.Throw<NotSupportedException>(() => sut.Position = 0);
    }

    [Fact]
    public async Task CompleteAsync_ShipsExactByteSequence_ForMultipleWrites()
    {
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();
        CapturedBytes captured = CaptureSavedBytes(provider);

        await using BufferedMultipartWriteStream sut = CreateSut(provider);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.WriteAsync(new byte[] { 4, 5, 6 }, TestContext.Current.CancellationToken);
        await sut.WriteAsync(new byte[] { 7, 8 }, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        captured.Value.ShouldBe([1, 2, 3, 4, 5, 6, 7, 8]);
    }

    // Reference-type holder so the lambda's assignment is observable from the test
    // after the SUT (and its temp-file buffer) has been disposed.
    private sealed class CapturedBytes
    {
        public byte[] Value { get; set; } = [];
    }

    private static CapturedBytes CaptureSavedBytes(IBlobStoreProvider provider)
    {
        CapturedBytes captured = new();
        provider.SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                Stream s = callInfo.Arg<Stream>();
                await using MemoryStream ms = new();
                s.CopyTo(ms);
                captured.Value = ms.ToArray();
                await Task.CompletedTask;
            });
        return captured;
    }
}
