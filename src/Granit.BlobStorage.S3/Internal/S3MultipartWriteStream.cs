using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using Granit.BlobStorage.S3.Diagnostics;

namespace Granit.BlobStorage.S3.Internal;

/// <summary>
/// Native S3 multipart upload — buffers each part in memory (default 8 MB) and ships
/// it via <see cref="IAmazonS3.UploadPartAsync(UploadPartRequest, CancellationToken)"/>
/// as soon as the threshold is hit, instead of the framework's default of buffering
/// the whole blob to a temp file before a single <see cref="IAmazonS3.PutObjectAsync(PutObjectRequest, CancellationToken)"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it matters.</b> The <c>BufferedMultipartWriteStream</c> fallback works for
/// every provider but pushes multi-gigabyte writes through the host's <c>%TMP%</c>
/// directory. Personal-data exports for active users can hit tens of gigabytes; the
/// temp-file shifts the bottleneck from RAM to disk pressure rather than removing
/// it. Native S3 multipart keeps each part in RAM for at most one part window
/// (default 8 MB), uploads it, and discards — disk usage stays flat.
/// </para>
/// <para>
/// <b>S3 constraints.</b> Each non-final part MUST be ≥ 5 MB
/// (<see href="https://docs.aws.amazon.com/AmazonS3/latest/userguide/qfacts.html">S3 spec</see>);
/// the final part can be any size. The stream enforces the minimum by waiting for the
/// in-memory buffer to reach <see cref="DefaultPartSizeBytes"/> before flushing — small
/// writes coalesce into a single part. A single-part upload (Complete called before
/// the buffer ever reaches the threshold) flushes the buffered bytes as the only
/// part, which S3 accepts.
/// </para>
/// <para>
/// <b>Lifecycle contract.</b>
/// </para>
/// <list type="bullet">
///   <item><see cref="CompleteAsync"/> uploads any remaining bytes as the final part
///         then issues <c>CompleteMultipartUpload</c>.</item>
///   <item><see cref="AbortAsync"/> issues <c>AbortMultipartUpload</c> — the bucket
///         lifecycle rule (<c>abort_incomplete_multipart_upload</c>) covers the case
///         where this never fires (process crash, network partition).</item>
///   <item>Disposing without calling either is treated as an abort.</item>
/// </list>
/// </remarks>
internal sealed class S3MultipartWriteStream : MultipartWriteStream
{
    /// <summary>Default S3 part size (8 MB). Configurable later if a host needs tuning.</summary>
    internal const int DefaultPartSizeBytes = 8 * 1024 * 1024;

    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly string _objectKey;
    private readonly string _uploadId;
    private readonly int _partSizeBytes;
    private readonly List<PartETag> _completedParts = [];

    private MemoryStream _partBuffer;
    private int _nextPartNumber = 1;
    private bool _completed;
    private bool _aborted;

    public S3MultipartWriteStream(
        IAmazonS3 s3,
        string bucket,
        string objectKey,
        string uploadId,
        int partSizeBytes = DefaultPartSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(s3);
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentException.ThrowIfNullOrEmpty(objectKey);
        ArgumentException.ThrowIfNullOrEmpty(uploadId);
        ArgumentOutOfRangeException.ThrowIfLessThan(partSizeBytes, 5 * 1024 * 1024);

        _s3 = s3;
        _bucket = bucket;
        _objectKey = objectKey;
        _uploadId = uploadId;
        _partSizeBytes = partSizeBytes;
        _partBuffer = new MemoryStream(partSizeBytes);
    }

    // ── Stream surface (write-only) ──────────────────────────────────────────

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !_aborted && !_completed;
    public override long Length => _partBuffer.Length;

    public override long Position
    {
        get => _partBuffer.Position;
        set => throw new NotSupportedException("S3 multipart write stream is forward-only.");
    }

    public override void Flush() { /* No flush on partial part — the buffer holds it until threshold. */ }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("S3 multipart write stream is write-only.");

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("S3 multipart write stream is forward-only.");

    public override void SetLength(long value) =>
        throw new NotSupportedException("S3 multipart write stream is forward-only.");

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfCompletedOrAborted();
        _partBuffer.Write(buffer, offset, count);
        if (_partBuffer.Length >= _partSizeBytes)
        {
            // Sync write path: flushing requires a synchronous network call. Re-running
            // the async path is the cleanest option since S3 multipart upload is an
            // inherently network-bound operation.
            FlushBufferAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfCompletedOrAborted();
        await _partBuffer.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        if (_partBuffer.Length >= _partSizeBytes)
        {
            await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfCompletedOrAborted();
        await _partBuffer.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (_partBuffer.Length >= _partSizeBytes)
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
                "Cannot complete an S3 multipart upload that has already been aborted.");
        }

        using Activity? activity = BlobStorageS3ActivitySource.Source.StartActivity("S3.MultipartComplete");
        activity?.SetTag(BlobStorageS3ActivitySource.TagBucket, _bucket);
        activity?.SetTag(BlobStorageS3ActivitySource.TagObjectKey, _objectKey);

        // Flush any remaining bytes as the final part. The last part has no minimum
        // size — even an empty trailing flush is acceptable so long as at least one
        // part exists (S3 rejects an empty multipart upload at Complete time).
        if (_partBuffer.Length > 0 || _completedParts.Count == 0)
        {
            await FlushBufferAsync(cancellationToken, isFinalPart: true).ConfigureAwait(false);
        }

        CompleteMultipartUploadRequest completeRequest = new()
        {
            BucketName = _bucket,
            Key = _objectKey,
            UploadId = _uploadId,
            PartETags = [.. _completedParts],
        };
        await _s3.CompleteMultipartUploadAsync(completeRequest, cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    public override async Task AbortAsync(CancellationToken cancellationToken = default)
    {
        if (_aborted || _completed)
        {
            return;
        }
        _aborted = true;

        using Activity? activity = BlobStorageS3ActivitySource.Source.StartActivity("S3.MultipartAbort");
        activity?.SetTag(BlobStorageS3ActivitySource.TagBucket, _bucket);
        activity?.SetTag(BlobStorageS3ActivitySource.TagObjectKey, _objectKey);

        try
        {
            await _s3.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = _bucket,
                Key = _objectKey,
                UploadId = _uploadId,
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort abort. The bucket's `abort_incomplete_multipart_upload`
            // lifecycle rule is the safety net for cases where this fails (network
            // partition, credentials lost).
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_completed && !_aborted)
        {
            await AbortAsync().ConfigureAwait(false);
        }

        await _partBuffer.DisposeAsync().ConfigureAwait(false);
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
                    _s3.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                    {
                        BucketName = _bucket,
                        Key = _objectKey,
                        UploadId = _uploadId,
                    }, CancellationToken.None).GetAwaiter().GetResult();
                }
                catch
                {
                    // Best-effort — see AbortAsync remarks.
                }
            }

            _partBuffer.Dispose();
        }

        base.Dispose(disposing);
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private async Task FlushBufferAsync(CancellationToken cancellationToken, bool isFinalPart = false)
    {
        _partBuffer.Position = 0;
        long partLength = _partBuffer.Length;

        UploadPartRequest uploadRequest = new()
        {
            BucketName = _bucket,
            Key = _objectKey,
            UploadId = _uploadId,
            PartNumber = _nextPartNumber,
            InputStream = _partBuffer,
            PartSize = partLength,
            IsLastPart = isFinalPart,
        };

        using (Activity? activity = BlobStorageS3ActivitySource.Source.StartActivity("S3.UploadPart"))
        {
            activity?.SetTag(BlobStorageS3ActivitySource.TagBucket, _bucket);
            activity?.SetTag(BlobStorageS3ActivitySource.TagObjectKey, _objectKey);
            activity?.SetTag("s3.part_number", _nextPartNumber);
            activity?.SetTag("s3.part_size_bytes", partLength);

            UploadPartResponse response = await _s3
                .UploadPartAsync(uploadRequest, cancellationToken)
                .ConfigureAwait(false);

            _completedParts.Add(new PartETag(_nextPartNumber, response.ETag));
        }

        _nextPartNumber++;

        // Recycle the buffer — Position=0 + SetLength(0) would keep the underlying
        // capacity, but Dispose+new releases it so a one-off 100 MB upload doesn't
        // permanently retain 8 MB.
        await _partBuffer.DisposeAsync().ConfigureAwait(false);
        _partBuffer = new MemoryStream(_partSizeBytes);
    }

    private void ThrowIfCompletedOrAborted()
    {
        ObjectDisposedException.ThrowIf(_aborted, this);
        if (_completed)
        {
            throw new InvalidOperationException(
                "Cannot write to an S3 multipart write stream after CompleteAsync.");
        }
    }
}
