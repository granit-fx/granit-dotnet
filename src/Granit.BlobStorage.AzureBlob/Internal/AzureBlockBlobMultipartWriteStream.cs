using System.Diagnostics;
using Granit.BlobStorage.AzureBlob.Diagnostics;

namespace Granit.BlobStorage.AzureBlob.Internal;

/// <summary>
/// Native Azure Block Blob multipart upload — buffers each block in memory
/// (default 8 MB) and stages it via <see cref="IAzureBlockBlobOperations.StageBlockAsync"/>
/// as soon as the threshold is hit, instead of the framework default that
/// buffers the whole blob to a temp file before a single
/// <c>IBlobStoreProvider.SaveAsync</c> call.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it matters.</b> Personal-data exports for active users can hit tens of
/// gigabytes; the temp-file fallback shifts the bottleneck from RAM to host
/// disk pressure rather than removing it. Native StageBlock keeps at most one
/// block window in RAM (default 8 MB) per concurrent upload.
/// </para>
/// <para>
/// <b>Block-id discipline.</b> Azure requires every block id committed in a
/// single <c>CommitBlockList</c> to share the same byte length. The stream
/// generates ids from <see cref="Guid.NewGuid"/> base64-encoded (24 chars
/// each) so the uniformity rule holds without per-call validation.
/// </para>
/// <para>
/// <b>No native abort.</b> Azure block blobs don't expose an abort call —
/// uncommitted blocks expire automatically after 7 days per the service
/// contract. <see cref="AbortAsync"/> simply skips
/// <c>CommitBlockListAsync</c>; the storage account's lifecycle policy is
/// the safety net for stuck stages.
/// </para>
/// <para>
/// <b>Lifecycle contract.</b>
/// </para>
/// <list type="bullet">
///   <item><see cref="CompleteAsync"/> stages any remaining bytes as the final
///         block then issues <c>CommitBlockList</c>.</item>
///   <item><see cref="AbortAsync"/> is a no-op except for the in-process flag
///         flip — staged blocks expire on the service side.</item>
///   <item>Disposing without calling either is treated as an abort.</item>
/// </list>
/// </remarks>
internal sealed class AzureBlockBlobMultipartWriteStream : MultipartWriteStream
{
    /// <summary>Default block size (8 MB). Aligned with the S3 default.</summary>
    internal const int DefaultBlockSizeBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Practical minimum block size. Azure has no hard minimum like S3 but
    /// going below 1 MB blows the 50 000-block ceiling on multi-GB uploads.
    /// </summary>
    internal const int MinBlockSizeBytes = 1 * 1024 * 1024;

    private readonly IAzureBlockBlobOperations _operations;
    private readonly string _contentType;
    private readonly int _blockSizeBytes;
    private readonly List<string> _blockIds = [];

    private MemoryStream _blockBuffer;
    private bool _completed;
    private bool _aborted;

    public AzureBlockBlobMultipartWriteStream(
        IAzureBlockBlobOperations operations,
        string contentType,
        int blockSizeBytes = DefaultBlockSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentException.ThrowIfNullOrEmpty(contentType);
        ArgumentOutOfRangeException.ThrowIfLessThan(blockSizeBytes, MinBlockSizeBytes);

        _operations = operations;
        _contentType = contentType;
        _blockSizeBytes = blockSizeBytes;
        _blockBuffer = new MemoryStream(blockSizeBytes);
    }

    // ── Stream surface (write-only) ──────────────────────────────────────────

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !_aborted && !_completed;
    public override long Length => _blockBuffer.Length;

    public override long Position
    {
        get => _blockBuffer.Position;
        set => throw new NotSupportedException("Azure block-blob multipart write stream is forward-only.");
    }

    public override void Flush() { /* No flush on partial block — the buffer holds it until threshold. */ }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Azure block-blob multipart write stream is write-only.");

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("Azure block-blob multipart write stream is forward-only.");

    public override void SetLength(long value) =>
        throw new NotSupportedException("Azure block-blob multipart write stream is forward-only.");

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfCompletedOrAborted();
        _blockBuffer.Write(buffer, offset, count);
        if (_blockBuffer.Length >= _blockSizeBytes)
        {
            // Sync path: StageBlock is an HTTP call and the stream API requires it
            // to run synchronously here. Most production callers use WriteAsync.
            FlushBufferAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfCompletedOrAborted();
        await _blockBuffer.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        if (_blockBuffer.Length >= _blockSizeBytes)
        {
            await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfCompletedOrAborted();
        await _blockBuffer.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (_blockBuffer.Length >= _blockSizeBytes)
        {
            await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
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
                "Cannot complete an Azure block-blob multipart upload that has already been aborted.");
        }

        using Activity? activity = BlobStorageAzureActivitySource.Source.StartActivity("Azure.CommitBlockList");

        // Stage any remaining bytes as the final block. Azure requires at least one
        // block committed; a zero-byte trailing block is acceptable.
        if (_blockBuffer.Length > 0 || _blockIds.Count == 0)
        {
            await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
        }

        await _operations.CommitBlockListAsync(_blockIds, _contentType, cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    public override Task AbortAsync(CancellationToken cancellationToken = default)
    {
        if (!_completed)
        {
            _aborted = true;
        }
        // Azure has no native abort. Staged blocks expire after 7 days by service
        // contract; the storage account lifecycle policy is the safety net.
        return Task.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_completed && !_aborted)
        {
            await AbortAsync().ConfigureAwait(false);
        }

        await _blockBuffer.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_completed && !_aborted)
            {
                _aborted = true;
            }

            _blockBuffer.Dispose();
        }

        base.Dispose(disposing);
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private async Task FlushBufferAsync(CancellationToken cancellationToken)
    {
        // Guid.NewGuid (not Granit.Guids.IGuidGenerator) is intentional here: the
        // block id is a per-upload local-only multipart token, not a database key —
        // no clustered-index ordering concern applies. Suppress GRSEC002.
        // Block ids must share the same byte length across a CommitBlockList call.
        // A Guid base64-encoded is always 24 chars — pinned uniformity, trivially unique.
#pragma warning disable GRSEC002
        string blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
#pragma warning restore GRSEC002
        _blockBuffer.Position = 0;
        long blockLength = _blockBuffer.Length;

        using (Activity? activity = BlobStorageAzureActivitySource.Source.StartActivity("Azure.StageBlock"))
        {
            activity?.SetTag("azure.block_id", blockId);
            activity?.SetTag("azure.block_size_bytes", blockLength);
            await _operations.StageBlockAsync(blockId, _blockBuffer, cancellationToken).ConfigureAwait(false);
        }

        _blockIds.Add(blockId);

        // Recycle the buffer — a one-off large upload shouldn't permanently retain
        // its capacity allocation.
        await _blockBuffer.DisposeAsync().ConfigureAwait(false);
        _blockBuffer = new MemoryStream(_blockSizeBytes);
    }

    private void ThrowIfCompletedOrAborted()
    {
        ObjectDisposedException.ThrowIf(_aborted, this);
        if (_completed)
        {
            throw new InvalidOperationException(
                "Cannot write to an Azure block-blob multipart write stream after CompleteAsync.");
        }
    }
}
