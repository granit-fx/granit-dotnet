using System.IO.Compression;
using Granit.BlobStorage;
using Granit.BlobStorage.Internal;
using Granit.Privacy.BlobStorage.DataExport.Internal;
using Granit.Privacy.DataExport.Sanitization;

namespace Granit.Privacy.BlobStorage.DataExport;

/// <summary>
/// Streams personal-data fragments into a sequence of size-capped ZIP shards uploaded
/// via <see cref="IBlobStoreProvider.OpenWriteMultipartAsync"/>. Each shard is a
/// self-contained ZIP64-capable archive named
/// <c>{objectKeyPrefix}-{shardIndex:D3}.zip</c>; the writer rolls over once a shard's
/// compressed footprint reaches the configured cap.
/// </summary>
/// <remarks>
/// <para>
/// <b>ZIP64.</b> Entries are created via <see cref="ZipArchive.CreateEntry(string, CompressionLevel)"/>,
/// which transparently writes ZIP64 extension fields for entries &gt; 4 GB on .NET 10 —
/// no opt-in flag required.
/// </para>
/// <para>
/// <b>Compression.</b> The per-entry compression level is chosen from the entry's
/// <c>contentType</c>: already-compressed payloads (PDF, PNG, JPEG, MP4,
/// ZIP, gzip, octet-stream) ship with <see cref="CompressionLevel.NoCompression"/>
/// (store mode) to avoid burning CPU on incompressible bytes. JSON/text/XML use
/// <see cref="CompressionLevel.Optimal"/>; everything else falls back to
/// <see cref="CompressionLevel.Fastest"/>.
/// </para>
/// <para>
/// <b>Sharding policy.</b> A new shard is opened lazily on first
/// <see cref="AppendAsync"/>. The current shard rolls over <i>before</i> appending an
/// entry whenever the compressed bytes already written exceed the cap — a single
/// entry larger than the cap is therefore allowed to land alone in its own shard
/// (ZIP64 covers it) rather than being split mid-stream.
/// </para>
/// <para>
/// <b>Path safety.</b> <see cref="EntryPathSanitizer.Sanitize"/> is invoked on every
/// entry path before <c>CreateEntry</c> — zip-slip and reserved-name violations fail
/// fast with <c>InvalidExportEntryPathException</c> rather than poisoning the shard.
/// </para>
/// <para>
/// <b>Disposal contract.</b> <see cref="CompleteAsync"/> finalises the open shard's
/// multipart upload and returns the manifest of every shard written. Disposing the
/// writer without calling <see cref="CompleteAsync"/> aborts the in-flight shard —
/// previously completed shards stay in storage and remain individually addressable.
/// </para>
/// </remarks>
internal sealed class ShardingArchiveWriter : IAsyncDisposable
{
    private readonly IBlobStoreProvider _provider;
    private readonly string _bucket;
    private readonly string _objectKeyPrefix;
    private readonly long _shardMaxBytes;
    private readonly List<ShardManifest> _completedShards = [];

    private int _nextShardIndex;
    private MultipartWriteStream? _multipart;
    private WriteCountingStream? _counter;
    private ZipArchive? _zip;
    private bool _completed;
    private bool _disposed;

    public ShardingArchiveWriter(
        IBlobStoreProvider provider,
        string bucket,
        string objectKeyPrefix,
        long shardMaxBytes)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentException.ThrowIfNullOrEmpty(objectKeyPrefix);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardMaxBytes);

        _provider = provider;
        _bucket = bucket;
        _objectKeyPrefix = objectKeyPrefix;
        _shardMaxBytes = shardMaxBytes;
    }

    /// <summary>Shards finalised by <see cref="CompleteAsync"/>, in write order.</summary>
    public IReadOnlyList<ShardManifest> Shards => _completedShards;

    /// <summary>
    /// Writes a single entry to the archive. Opens a new shard lazily on the first
    /// call; rolls the current shard over before writing whenever its compressed
    /// footprint already meets or exceeds the cap.
    /// </summary>
    public async Task AppendAsync(
        string entryPath,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed)
        {
            throw new InvalidOperationException(
                "Cannot append to a sharding archive writer after CompleteAsync.");
        }

        ArgumentException.ThrowIfNullOrEmpty(entryPath);
        ArgumentException.ThrowIfNullOrEmpty(contentType);
        ArgumentNullException.ThrowIfNull(content);

        string safeEntryPath = EntryPathSanitizer.Sanitize(entryPath);

        if (_zip is not null && _counter!.BytesWritten >= _shardMaxBytes)
        {
            await CloseCurrentShardAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_zip is null)
        {
            await OpenNewShardAsync(cancellationToken).ConfigureAwait(false);
        }

        CompressionLevel level = PickCompressionLevel(contentType);
        ZipArchiveEntry entry = _zip!.CreateEntry(safeEntryPath, level);
        await using Stream entryStream = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
        await content.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finalises the open shard (if any), uploads it, and returns the full manifest
    /// of shards written. Idempotent: a second call is a no-op.
    /// </summary>
    public async Task<IReadOnlyList<ShardManifest>> CompleteAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed)
        {
            return _completedShards;
        }

        if (_zip is not null)
        {
            await CloseCurrentShardAsync(cancellationToken).ConfigureAwait(false);
        }

        _completed = true;
        return _completedShards;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        // Implicit abort path: the open shard (if any) is discarded. Previously
        // completed shards stay committed in storage.
        if (_zip is not null)
        {
            try { _zip.Dispose(); }
            catch { /* ignore — abort path */ }
            _zip = null;
        }

        if (_multipart is not null)
        {
            try { await _multipart.AbortAsync().ConfigureAwait(false); }
            catch { /* ignore — abort path */ }
            await _multipart.DisposeAsync().ConfigureAwait(false);
            _multipart = null;
        }

        if (_counter is not null)
        {
            await _counter.DisposeAsync().ConfigureAwait(false);
            _counter = null;
        }
    }

    private async Task OpenNewShardAsync(CancellationToken cancellationToken)
    {
        string objectKey = $"{_objectKeyPrefix}-{_nextShardIndex:D3}.zip";
        _multipart = await _provider.OpenWriteMultipartAsync(
            _bucket, objectKey, "application/zip", cancellationToken).ConfigureAwait(false);
        _counter = new WriteCountingStream(_multipart, leaveOpen: true);
        // leaveOpen on the ZipArchive so disposing it writes the central directory
        // through the counter without closing the multipart stream — we still need
        // to call CompleteAsync on it explicitly.
        _zip = new ZipArchive(_counter, ZipArchiveMode.Create, leaveOpen: true);
    }

    private async Task CloseCurrentShardAsync(CancellationToken cancellationToken)
    {
        if (_zip is null)
        {
            return;
        }

        int shardIndex = _nextShardIndex;
        string objectKey = $"{_objectKeyPrefix}-{shardIndex:D3}.zip";

        // Disposing ZipArchive writes the central directory bytes into the counter +
        // multipart stream. Must precede CompleteAsync on the multipart so the upload
        // captures the directory.
        _zip.Dispose();
        _zip = null;

        long bytes = _counter!.BytesWritten;
        await _counter.FlushAsync(cancellationToken).ConfigureAwait(false);
        await _multipart!.CompleteAsync(cancellationToken).ConfigureAwait(false);

        await _counter.DisposeAsync().ConfigureAwait(false);
        _counter = null;
        await _multipart.DisposeAsync().ConfigureAwait(false);
        _multipart = null;

        _completedShards.Add(new ShardManifest(shardIndex, objectKey, bytes));
        _nextShardIndex++;
    }

    internal static CompressionLevel PickCompressionLevel(string contentType)
    {
        // Lowercase, strip parameters like "; charset=utf-8".
        ReadOnlySpan<char> ct = contentType.AsSpan();
        int semi = ct.IndexOf(';');
        if (semi >= 0)
        {
            ct = ct[..semi];
        }

        ct = ct.Trim();
        Span<char> lower = stackalloc char[ct.Length];
        ct.ToLowerInvariant(lower);
        ReadOnlySpan<char> normalised = lower;

        // Already-compressed payloads → store mode. Burning CPU on these is pure waste.
        if (normalised.SequenceEqual("application/pdf")
            || normalised.SequenceEqual("application/zip")
            || normalised.SequenceEqual("application/gzip")
            || normalised.SequenceEqual("application/x-gzip")
            || normalised.SequenceEqual("application/x-7z-compressed")
            || normalised.SequenceEqual("application/octet-stream")
            || normalised.StartsWith("image/")
            || normalised.StartsWith("video/")
            || normalised.StartsWith("audio/"))
        {
            return CompressionLevel.NoCompression;
        }

        // Textual content compresses well, and JSON/XML/CSV dominate framework providers.
        if (normalised.SequenceEqual("application/json")
            || normalised.SequenceEqual("application/xml")
            || normalised.StartsWith("text/"))
        {
            return CompressionLevel.Optimal;
        }

        return CompressionLevel.Fastest;
    }
}

/// <summary>
/// Metadata for a finalised shard returned by
/// <see cref="ShardingArchiveWriter.CompleteAsync"/>.
/// </summary>
/// <param name="Index">Zero-based shard index in write order.</param>
/// <param name="ObjectKey">Blob-storage object key of the shard ZIP.</param>
/// <param name="CompressedSizeBytes">Total bytes written to the shard (post-ZIP framing).</param>
internal sealed record ShardManifest(int Index, string ObjectKey, long CompressedSizeBytes);
