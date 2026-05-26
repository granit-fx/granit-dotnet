using System.Diagnostics;
using Granit.BlobStorage.GoogleCloud.Diagnostics;

namespace Granit.BlobStorage.GoogleCloud.Internal;

/// <summary>
/// Native GCS resumable upload — buffers each chunk in memory (default 8 MB)
/// and ships it via <see cref="IGcsResumableUploadOperations.UploadChunkAsync"/>
/// as soon as the threshold is hit, instead of the framework default that
/// buffers the whole blob to a temp file before a single
/// <c>IBlobStoreProvider.SaveAsync</c> call.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it matters.</b> Personal-data exports for active users can hit tens of
/// gigabytes; the temp-file fallback shifts the bottleneck from RAM to host
/// disk pressure rather than removing it. Native resumable upload keeps at most
/// one chunk window in RAM (default 8 MB) per concurrent upload.
/// </para>
/// <para>
/// <b>Chunk size discipline.</b> GCS requires every chunk except the last to be
/// a multiple of 256 KiB (262,144 bytes); the final chunk can be any size.
/// The stream enforces this two ways: (1) the constructor rejects chunk sizes
/// that aren't a multiple of 256 KiB, and (2) writes that would overflow the
/// chunk size are split so the in-memory buffer never exceeds it before flush.
/// </para>
/// <para>
/// <b>Abort semantics.</b> The session URI is the abort target — DELETE on
/// that URI discards any uploaded chunks. The bucket's
/// <c>abort_incomplete_multipart_upload</c> lifecycle rule is the safety net
/// for the case where the abort itself fails (network partition, process kill).
/// </para>
/// <para>
/// <b>Lifecycle contract.</b>
/// </para>
/// <list type="bullet">
///   <item><see cref="CompleteAsync"/> uploads any remaining buffered bytes as
///         the final chunk with the total object size, which commits the upload.</item>
///   <item><see cref="AbortAsync"/> issues DELETE against the session URI —
///         best-effort.</item>
///   <item>Disposing without calling either is treated as an abort.</item>
/// </list>
/// </remarks>
internal sealed class GcsResumableMultipartWriteStream : MultipartWriteStream
{
    /// <summary>Default chunk size (8 MB = 32 × 256 KiB). Aligned with the S3 default.</summary>
    internal const int DefaultChunkSizeBytes = 8 * 1024 * 1024;

    /// <summary>
    /// GCS-mandated chunk-size alignment: every non-final chunk must be a multiple
    /// of 256 KiB. Configured chunk sizes must therefore be a multiple of this value.
    /// </summary>
    internal const int MinChunkSizeBytes = 256 * 1024;

    private readonly IGcsResumableUploadOperations _operations;
    private readonly Uri _sessionUri;
    private readonly int _chunkSizeBytes;

    private MemoryStream _chunkBuffer;
    private long _bytesUploaded;
    private bool _completed;
    private bool _aborted;

    public GcsResumableMultipartWriteStream(
        IGcsResumableUploadOperations operations,
        Uri sessionUri,
        int chunkSizeBytes = DefaultChunkSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(sessionUri);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSizeBytes, MinChunkSizeBytes);
        if (chunkSizeBytes % MinChunkSizeBytes != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkSizeBytes),
                $"GCS resumable upload chunk size must be a multiple of {MinChunkSizeBytes} (256 KiB).");
        }

        _operations = operations;
        _sessionUri = sessionUri;
        _chunkSizeBytes = chunkSizeBytes;
        _chunkBuffer = new MemoryStream(chunkSizeBytes);
    }

    // ── Stream surface (write-only) ──────────────────────────────────────────

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !_aborted && !_completed;
    public override long Length => _bytesUploaded + _chunkBuffer.Length;

    public override long Position
    {
        get => Length;
        set => throw new NotSupportedException("GCS resumable upload stream is forward-only.");
    }

    public override void Flush() { /* No flush on partial chunk — the buffer holds it until threshold. */ }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("GCS resumable upload stream is write-only.");

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("GCS resumable upload stream is forward-only.");

    public override void SetLength(long value) =>
        throw new NotSupportedException("GCS resumable upload stream is forward-only.");

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfCompletedOrAborted();
        WriteCoreAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfCompletedOrAborted();
        return WriteCoreAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfCompletedOrAborted();
        return WriteCoreAsync(buffer, cancellationToken);
    }

    private async ValueTask WriteCoreAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        // GCS requires non-final chunks to be exactly a multiple of 256 KiB. The
        // cleanest way to honour that is to never let the in-memory buffer exceed
        // _chunkSizeBytes — split overflowing writes and flush at the boundary.
        int remaining = buffer.Length;
        int offset = 0;
        while (remaining > 0)
        {
            int spaceLeft = _chunkSizeBytes - (int)_chunkBuffer.Length;
            int toWrite = Math.Min(spaceLeft, remaining);
            await _chunkBuffer.WriteAsync(buffer.Slice(offset, toWrite), cancellationToken).ConfigureAwait(false);
            offset += toWrite;
            remaining -= toWrite;

            if (_chunkBuffer.Length == _chunkSizeBytes)
            {
                await FlushBufferAsync(isFinal: false, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // ── Multipart lifecycle ──────────────────────────────────────────────────

    public override async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            return;
        }

        if (_aborted)
        {
            throw new InvalidOperationException(
                "Cannot complete a GCS resumable upload that has already been aborted.");
        }

        using Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity("Gcs.CompleteResumableUpload");

        // The final chunk carries the total object size, which is what commits the
        // upload on the GCS side. An empty trailing chunk is acceptable — that's
        // how a zero-byte object is finalised.
        await FlushBufferAsync(isFinal: true, cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    public override async Task AbortAsync(CancellationToken cancellationToken = default)
    {
        if (_aborted || _completed)
        {
            return;
        }
        _aborted = true;

        using Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity("Gcs.AbortResumableUpload");
        await _operations.AbortSessionAsync(_sessionUri, cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_completed && !_aborted)
        {
            await AbortAsync().ConfigureAwait(false);
        }

        await _chunkBuffer.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_completed && !_aborted)
            {
                _aborted = true;
                try
                {
                    _operations.AbortSessionAsync(_sessionUri, CancellationToken.None).GetAwaiter().GetResult();
                }
                catch
                {
                    // Best-effort abort — see AbortAsync remarks.
                }
            }

            _chunkBuffer.Dispose();
        }

        base.Dispose(disposing);
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private async Task FlushBufferAsync(bool isFinal, CancellationToken cancellationToken)
    {
        long chunkLength = _chunkBuffer.Length;
        long? totalSize = isFinal ? _bytesUploaded + chunkLength : null;
        ReadOnlyMemory<byte> chunkBytes = _chunkBuffer.GetBuffer().AsMemory(0, (int)chunkLength);

        using (Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity("Gcs.UploadChunk"))
        {
            activity?.SetTag("gcs.chunk_offset", _bytesUploaded);
            activity?.SetTag("gcs.chunk_size_bytes", chunkLength);
            activity?.SetTag("gcs.is_final", isFinal);
            await _operations.UploadChunkAsync(_sessionUri, chunkBytes, _bytesUploaded, totalSize, cancellationToken).ConfigureAwait(false);
        }

        _bytesUploaded += chunkLength;

        // Recycle the buffer — a one-off large upload shouldn't permanently retain
        // its capacity allocation.
        await _chunkBuffer.DisposeAsync().ConfigureAwait(false);
        _chunkBuffer = new MemoryStream(_chunkSizeBytes);
    }

    private void ThrowIfCompletedOrAborted()
    {
        ObjectDisposedException.ThrowIf(_aborted, this);
        if (_completed)
        {
            throw new InvalidOperationException(
                "Cannot write to a GCS resumable upload stream after CompleteAsync.");
        }
    }
}
