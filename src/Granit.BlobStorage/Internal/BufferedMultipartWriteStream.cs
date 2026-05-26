namespace Granit.BlobStorage.Internal;

/// <summary>
/// Default <see cref="MultipartWriteStream"/> — buffers writes to a temp file
/// (FileOptions.DeleteOnClose) and ships the whole content via a single
/// <see cref="IBlobStoreProvider.SaveAsync"/> call on
/// <see cref="MultipartWriteStream.CompleteAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Correct for every provider — used as the default implementation behind the
/// default interface method on <see cref="IBlobStoreProvider"/>. Providers with
/// native multipart support (S3 InitiateMultipartUpload, Azure Block Blob
/// StageBlock, GCS resumable upload) override
/// <see cref="IBlobStoreProvider.OpenWriteMultipartAsync"/> for memory-efficient
/// streaming on multi-GB blobs — the temp file shifts to host disk pressure
/// but never touches RAM beyond the FileStream buffer.
/// </para>
/// <para>
/// <b>Disposal contract:</b> calling <see cref="DisposeAsync"/> (or sync
/// <c>Dispose</c>) without first calling
/// <see cref="MultipartWriteStream.CompleteAsync"/> implicitly aborts —
/// partial bytes never reach the blob.
/// </para>
/// </remarks>
internal sealed class BufferedMultipartWriteStream : MultipartWriteStream
{
    private readonly IBlobStoreProvider _provider;
    private readonly string _bucket;
    private readonly string _objectKey;
    private readonly string _contentType;
    private readonly FileStream _buffer;
    private bool _completed;
    private bool _aborted;

    public BufferedMultipartWriteStream(
        IBlobStoreProvider provider,
        string bucket,
        string objectKey,
        string contentType)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentException.ThrowIfNullOrEmpty(objectKey);
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        _provider = provider;
        _bucket = bucket;
        _objectKey = objectKey;
        _contentType = contentType;

        // Guid.NewGuid (not Granit.Guids.IGuidGenerator) is intentional here: the
        // temp filename is local-only and short-lived, not a database key — no
        // clustered-index ordering concern applies. Suppress GRSEC002.
#pragma warning disable GRSEC002
        string tempPath = Path.Combine(
            Path.GetTempPath(),
            $"granit-blobstorage-mpw-{Guid.NewGuid():N}.tmp");
#pragma warning restore GRSEC002
        _buffer = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Stream surface — forwarded to the temp-file buffer (write-only client view).
    // ────────────────────────────────────────────────────────────────────────

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !_aborted && !_completed;
    public override long Length => _buffer.Length;

    public override long Position
    {
        get => _buffer.Position;
        set => throw new NotSupportedException("Multipart write stream is forward-only.");
    }

    public override void Flush() => _buffer.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _buffer.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Multipart write stream is write-only.");

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("Multipart write stream is forward-only.");

    public override void SetLength(long value) =>
        throw new NotSupportedException("Multipart write stream is forward-only.");

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfCompletedOrAborted();
        _buffer.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfCompletedOrAborted();
        _buffer.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfCompletedOrAborted();
        return _buffer.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfCompletedOrAborted();
        return _buffer.WriteAsync(buffer, cancellationToken);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Multipart lifecycle.
    // ────────────────────────────────────────────────────────────────────────

    public override async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            return;
        }

        if (_aborted)
        {
            throw new InvalidOperationException(
                "Cannot complete a multipart write stream that has already been aborted.");
        }

        await _buffer.FlushAsync(cancellationToken).ConfigureAwait(false);
        _buffer.Position = 0;
        await _provider
            .SaveAsync(_bucket, _objectKey, _buffer, _contentType, cancellationToken)
            .ConfigureAwait(false);
        _completed = true;
    }

    public override Task AbortAsync(CancellationToken cancellationToken = default)
    {
        if (!_completed)
        {
            _aborted = true;
        }

        return Task.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_completed && !_aborted)
        {
            await AbortAsync().ConfigureAwait(false);
        }

        await _buffer.DisposeAsync().ConfigureAwait(false);
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

            _buffer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ThrowIfCompletedOrAborted()
    {
        ObjectDisposedException.ThrowIf(_aborted, this);
        if (_completed)
        {
            throw new InvalidOperationException(
                "Cannot write to a multipart write stream after CompleteAsync.");
        }
    }
}
