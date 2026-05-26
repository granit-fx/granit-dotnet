using Granit.BlobStorage.AzureBlob.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.AzureBlob.Tests.Internal;

public sealed class AzureBlockBlobMultipartWriteStreamTests
{
    private const string ContentType = "application/zip";
    private const int OneMb = 1 * 1024 * 1024;

    [Fact]
    public async Task CompleteAsync_SinglePayloadUnderBlockThreshold_StagesOneBlock_AndCommits()
    {
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();

        await using (AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb))
        {
            await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
            await sut.CompleteAsync(TestContext.Current.CancellationToken);
        }

        await ops.Received(1).StageBlockAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await ops.Received(1).CommitBlockListAsync(
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1),
            ContentType,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_AcrossMultipleBlocks_StagesMidStream_AndShipsFinalRemainder()
    {
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();

        await using AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb);
        // First write hits the threshold → stage as block 1.
        await sut.WriteAsync(new byte[OneMb], TestContext.Current.CancellationToken);

        await ops.Received(1).StageBlockAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());

        // Small follow-up rides Complete as block 2.
        await sut.WriteAsync(new byte[1024], TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await ops.Received(2).StageBlockAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await ops.Received(1).CommitBlockListAsync(
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 2),
            ContentType,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitBlockList_ReceivesBlockIdsInStageOrder()
    {
        List<string> stagedOrder = [];
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();
        ops.StageBlockAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                stagedOrder.Add(callInfo.Arg<string>());
                return Task.CompletedTask;
            });

        IReadOnlyList<string>? committed = null;
        ops.CommitBlockListAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                committed = callInfo.Arg<IReadOnlyList<string>>();
                return Task.CompletedTask;
            });

        await using AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb);
        await sut.WriteAsync(new byte[OneMb], TestContext.Current.CancellationToken);
        await sut.WriteAsync(new byte[OneMb], TestContext.Current.CancellationToken);
        await sut.WriteAsync(new byte[1024], TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        committed.ShouldNotBeNull();
        committed.ShouldBe(stagedOrder, "CommitBlockList must order block ids exactly as StageBlock saw them");
        committed.Count.ShouldBe(3);
    }

    [Fact]
    public async Task BlockIds_AreFixedLength()
    {
        // Azure rejects mixed-length block ids at CommitBlockList time. Pin the
        // 24-char base64-of-Guid uniformity.
        List<string> staged = [];
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();
        ops.StageBlockAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                staged.Add(callInfo.Arg<string>());
                return Task.CompletedTask;
            });

        await using AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb);
        await sut.WriteAsync(new byte[OneMb], TestContext.Current.CancellationToken);
        await sut.WriteAsync(new byte[OneMb], TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        staged.Count.ShouldBeGreaterThan(1);
        staged.Select(id => id.Length).Distinct().Count().ShouldBe(1);
        staged[0].Length.ShouldBe(24); // 16-byte Guid → 24 base64 chars.
    }

    [Fact]
    public async Task AbortAsync_DoesNotCommit()
    {
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();

        await using AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await ops.DidNotReceive().CommitBlockListAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisposeAsync_WithoutCompleteOrAbort_DoesNotCommit()
    {
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();

        await using (AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb))
        {
            await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
            // No explicit Complete or Abort.
        }

        await ops.DidNotReceive().CommitBlockListAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_IsIdempotent()
    {
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();

        await using AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await ops.Received(1).CommitBlockListAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_AfterAbort_Throws()
    {
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();

        await using AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CompleteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAsync_AfterComplete_Throws()
    {
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();

        await using AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.WriteAsync(new byte[] { 4 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAsync_AfterAbort_Throws()
    {
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();

        await using AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ObjectDisposedException>(
            async () => await sut.WriteAsync(new byte[] { 4 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void StreamSurface_RejectsReadsAndSeeks()
    {
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();
        using AzureBlockBlobMultipartWriteStream sut = new(ops, ContentType, blockSizeBytes: OneMb);

        sut.CanRead.ShouldBeFalse();
        sut.CanSeek.ShouldBeFalse();
        sut.CanWrite.ShouldBeTrue();

        Should.Throw<NotSupportedException>(() => sut.Read(new byte[4], 0, 4));
        Should.Throw<NotSupportedException>(() => sut.Seek(0, SeekOrigin.Begin));
        Should.Throw<NotSupportedException>(() => sut.SetLength(10));
        Should.Throw<NotSupportedException>(() => sut.Position = 0);
    }

    [Fact]
    public void Ctor_RejectsBlockSizeBelowMinimum()
    {
        IAzureBlockBlobOperations ops = Substitute.For<IAzureBlockBlobOperations>();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new AzureBlockBlobMultipartWriteStream(ops, ContentType, blockSizeBytes: 1));
    }
}
