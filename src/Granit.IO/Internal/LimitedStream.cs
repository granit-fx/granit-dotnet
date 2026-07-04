namespace Granit.IO.Internal;

/// <summary>
/// Stream wrapper that caps total writeable bytes. Reads and seeks pass through.
/// </summary>
/// <remarks>
/// Throws <see cref="IOException"/> when a write or <see cref="SetLength"/> call
/// would push the underlying stream beyond <c>MaxSizeBytes</c>.
/// </remarks>
internal sealed class LimitedStream : Stream
{
    private readonly Stream _inner;

    public LimitedStream(Stream inner, long maxSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSizeBytes);
        _inner = inner;
        MaxSizeBytes = maxSizeBytes;
    }

    public long MaxSizeBytes { get; }

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => _inner.CanWrite;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => _inner.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value)
    {
        EnsureWithinCap(value);
        _inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureWithinCap(_inner.Position + count);
        _inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureWithinCap(_inner.Position + buffer.Length);
        _inner.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        EnsureWithinCap(_inner.Position + count);
        return _inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureWithinCap(_inner.Position + buffer.Length);
        return _inner.WriteAsync(buffer, cancellationToken);
    }

    public override void WriteByte(byte value)
    {
        EnsureWithinCap(_inner.Position + 1);
        _inner.WriteByte(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private void EnsureWithinCap(long projectedSize)
    {
        if (projectedSize > MaxSizeBytes)
        {
            throw new IOException(
                $"Temp file exceeded MaxSizeBytes={MaxSizeBytes}.");
        }
    }
}
