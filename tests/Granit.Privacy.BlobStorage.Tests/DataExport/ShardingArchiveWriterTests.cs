using System.IO.Compression;
using System.Text;
using Granit.BlobStorage.Internal;
using Granit.Privacy.BlobStorage.DataExport;
using Granit.Privacy.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BlobStorage.Tests.DataExport;

public sealed class ShardingArchiveWriterTests
{
    private const string Bucket = "gdpr-exports";
    private const string KeyPrefix = "tenant-a/personal-data-export-00000000";

    [Fact]
    public async Task CompleteAsync_SingleEntry_WritesOneShard_RoundTrips()
    {
        FakeBlobStoreProvider provider = new();
        byte[] payload = Encoding.UTF8.GetBytes("hello world");

        await using (ShardingArchiveWriter sut = new(provider, Bucket, KeyPrefix, shardMaxBytes: 10_000))
        {
            await sut.AppendAsync("identity-local.json", "application/json",
                new MemoryStream(payload), TestContext.Current.CancellationToken);
            IReadOnlyList<ShardManifest> shards = await sut.CompleteAsync(TestContext.Current.CancellationToken);

            shards.Count.ShouldBe(1);
            shards[0].Index.ShouldBe(0);
            shards[0].ObjectKey.ShouldBe($"{KeyPrefix}-000.zip");
        }

        provider.SavedBlobs.Count.ShouldBe(1);
        byte[] zipBytes = provider.SavedBlobs[$"{KeyPrefix}-000.zip"];

        using MemoryStream zipStream = new(zipBytes);
        using ZipArchive zip = new(zipStream, ZipArchiveMode.Read);
        zip.Entries.Count.ShouldBe(1);
        zip.Entries[0].FullName.ShouldBe("identity-local.json");

        using Stream entry = zip.Entries[0].Open();
        using MemoryStream copied = new();
        entry.CopyTo(copied);
        copied.ToArray().ShouldBe(payload);
    }

    [Fact]
    public async Task AppendAsync_RollsOverToNewShard_WhenCurrentExceedsCap()
    {
        FakeBlobStoreProvider provider = new();
        // Small cap forces a rollover after the first incompressible-ish entry.
        byte[] payload = new byte[3000];
        Random.Shared.NextBytes(payload);  // random → does not compress

        await using ShardingArchiveWriter sut = new(provider, Bucket, KeyPrefix, shardMaxBytes: 1024);

        await sut.AppendAsync("a.bin", "application/octet-stream",
            new MemoryStream(payload), TestContext.Current.CancellationToken);
        await sut.AppendAsync("b.bin", "application/octet-stream",
            new MemoryStream(payload), TestContext.Current.CancellationToken);

        IReadOnlyList<ShardManifest> shards = await sut.CompleteAsync(TestContext.Current.CancellationToken);

        shards.Count.ShouldBe(2);
        shards[0].Index.ShouldBe(0);
        shards[1].Index.ShouldBe(1);
        shards[0].ObjectKey.ShouldBe($"{KeyPrefix}-000.zip");
        shards[1].ObjectKey.ShouldBe($"{KeyPrefix}-001.zip");
        provider.SavedBlobs.Keys.ShouldContain($"{KeyPrefix}-000.zip");
        provider.SavedBlobs.Keys.ShouldContain($"{KeyPrefix}-001.zip");
    }

    [Fact]
    public async Task AppendAsync_KeepsLargeEntryInSingleShard_WhenItExceedsCap()
    {
        FakeBlobStoreProvider provider = new();
        byte[] payload = new byte[8000];
        Random.Shared.NextBytes(payload);

        await using ShardingArchiveWriter sut = new(provider, Bucket, KeyPrefix, shardMaxBytes: 1000);
        await sut.AppendAsync("solo.bin", "application/octet-stream",
            new MemoryStream(payload), TestContext.Current.CancellationToken);
        IReadOnlyList<ShardManifest> shards = await sut.CompleteAsync(TestContext.Current.CancellationToken);

        shards.Count.ShouldBe(1);
        shards[0].CompressedSizeBytes.ShouldBeGreaterThan(payload.Length);
    }

    [Fact]
    public async Task AppendAsync_SanitizesEntryPath()
    {
        FakeBlobStoreProvider provider = new();

        await using ShardingArchiveWriter sut = new(provider, Bucket, KeyPrefix, shardMaxBytes: 10_000);
        await Should.ThrowAsync<InvalidExportEntryPathException>(() =>
            sut.AppendAsync("../../etc/passwd", "application/octet-stream",
                new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisposeAsync_WithoutComplete_DoesNotPublishOpenShard()
    {
        FakeBlobStoreProvider provider = new();

        await using (ShardingArchiveWriter sut = new(provider, Bucket, KeyPrefix, shardMaxBytes: 10_000))
        {
            await sut.AppendAsync("a.json", "application/json",
                new MemoryStream(Encoding.UTF8.GetBytes("{}")), TestContext.Current.CancellationToken);
            // No Complete.
        }

        provider.SavedBlobs.ShouldBeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_KeepsAlreadyCompletedShards()
    {
        FakeBlobStoreProvider provider = new();
        byte[] payload = new byte[2000];
        Random.Shared.NextBytes(payload);

        await using ShardingArchiveWriter sut = new(provider, Bucket, KeyPrefix, shardMaxBytes: 1000);
        // First append rolls over, second opens a new shard but never completes it.
        await sut.AppendAsync("a.bin", "application/octet-stream",
            new MemoryStream(payload), TestContext.Current.CancellationToken);
        await sut.AppendAsync("b.bin", "application/octet-stream",
            new MemoryStream(payload), TestContext.Current.CancellationToken);
        // Dispose now without Complete: shard 001 is in-flight (>= cap not yet hit since it's
        // the only entry); shard 000 already completed when the rollover fired.
        await sut.DisposeAsync();

        provider.SavedBlobs.Keys.ShouldContain($"{KeyPrefix}-000.zip");
        provider.SavedBlobs.Keys.ShouldNotContain($"{KeyPrefix}-001.zip");
    }

    [Fact]
    public async Task CompleteAsync_IsIdempotent()
    {
        FakeBlobStoreProvider provider = new();

        await using ShardingArchiveWriter sut = new(provider, Bucket, KeyPrefix, shardMaxBytes: 10_000);
        await sut.AppendAsync("a.json", "application/json",
            new MemoryStream(Encoding.UTF8.GetBytes("{}")), TestContext.Current.CancellationToken);

        IReadOnlyList<ShardManifest> first = await sut.CompleteAsync(TestContext.Current.CancellationToken);
        IReadOnlyList<ShardManifest> second = await sut.CompleteAsync(TestContext.Current.CancellationToken);

        first.Count.ShouldBe(1);
        second.ShouldBeSameAs(first);
        provider.SavedBlobs.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AppendAsync_AfterComplete_Throws()
    {
        FakeBlobStoreProvider provider = new();

        await using ShardingArchiveWriter sut = new(provider, Bucket, KeyPrefix, shardMaxBytes: 10_000);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.AppendAsync("a.json", "application/json",
                new MemoryStream(Encoding.UTF8.GetBytes("{}")), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("application/json", CompressionLevel.Optimal)]
    [InlineData("application/xml", CompressionLevel.Optimal)]
    [InlineData("text/plain", CompressionLevel.Optimal)]
    [InlineData("text/csv", CompressionLevel.Optimal)]
    [InlineData("text/html; charset=utf-8", CompressionLevel.Optimal)]
    [InlineData("application/pdf", CompressionLevel.NoCompression)]
    [InlineData("application/zip", CompressionLevel.NoCompression)]
    [InlineData("application/gzip", CompressionLevel.NoCompression)]
    [InlineData("application/octet-stream", CompressionLevel.NoCompression)]
    [InlineData("image/png", CompressionLevel.NoCompression)]
    [InlineData("image/jpeg", CompressionLevel.NoCompression)]
    [InlineData("video/mp4", CompressionLevel.NoCompression)]
    [InlineData("audio/mpeg", CompressionLevel.NoCompression)]
    [InlineData("application/x-protobuf", CompressionLevel.Fastest)]
    public void PickCompressionLevel_MatchesContentTypeTable(string contentType, CompressionLevel expected) =>
        ShardingArchiveWriter.PickCompressionLevel(contentType).ShouldBe(expected);

    /// <summary>
    /// In-memory <see cref="IBlobStoreProvider"/> — exercises the default interface
    /// implementation of <see cref="IBlobStoreProvider.OpenWriteMultipartAsync"/>
    /// (which buffers to a temp file and ships via <see cref="IBlobStoreProvider.SaveAsync"/>).
    /// </summary>
    private sealed class FakeBlobStoreProvider : IBlobStoreProvider
    {
        public Dictionary<string, byte[]> SavedBlobs { get; } = [];

        public async Task SaveAsync(string bucket, string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
        {
            using MemoryStream ms = new();
            await content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            SavedBlobs[objectKey] = ms.ToArray();
        }

        public Task<Stream> OpenReadAsync(string bucket, string objectKey, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<long> GetSizeAsync(string bucket, string objectKey, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Stream> OpenPartialReadAsync(string bucket, string objectKey, int byteCount, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
