using System.Security.Cryptography;

namespace Granit.Privacy.BlobStorage.DataExport.Internal;

/// <summary>
/// Forward-only write-through SHA-256 digest. Mirrors
/// <see cref="WriteCountingStream"/>: every byte handed to the wrapped stream is
/// also fed into an <see cref="IncrementalHash"/>, so the shard's content
/// digest is available at close time without a second pass over the bytes.
/// </summary>
/// <remarks>
/// The shard pipeline layers the streams as
/// <c>ZipArchive → Sha256ComputingStream → WriteCountingStream → MultipartWriteStream</c>,
/// so the sha256 covers exactly the bytes that land in blob storage (post-ZIP
/// framing including the central directory). The digest is finalised by
/// <see cref="ComputeHashAndReset"/> just before the multipart upload is
/// committed.
/// </remarks>
internal sealed class Sha256ComputingStream(Stream inner, bool leaveOpen) : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool _disposed;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        _hash.AppendData(buffer, offset, count);
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _hash.AppendData(buffer);
        inner.Write(buffer);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _hash.AppendData(buffer, offset, count);
        await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _hash.AppendData(buffer.Span);
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override void WriteByte(byte value)
    {
        Span<byte> single = [value];
        _hash.AppendData(single);
        inner.WriteByte(value);
    }

    /// <summary>
    /// Finalises the running digest and returns its 32-byte SHA-256 result. Resets
    /// the hash so the same stream cannot be re-used for a second digest — call
    /// exactly once per shard, immediately before the multipart upload commits.
    /// </summary>
    public byte[] ComputeHashAndReset() => _hash.GetHashAndReset();

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);
            return;
        }
        _disposed = true;

        if (disposing)
        {
            _hash.Dispose();
            if (!leaveOpen)
            {
                inner.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            await base.DisposeAsync().ConfigureAwait(false);
            return;
        }
        _disposed = true;

        _hash.Dispose();
        if (!leaveOpen)
        {
            await inner.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
