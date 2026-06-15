namespace Granit.Privacy.BlobStorage.DataExport.Internal;

/// <summary>
/// Base for forward-only, write-through <see cref="Stream"/> decorators used by the
/// data-export shard pipeline. Implements the inert read/seek surface and forwards
/// every write to the wrapped stream, handing the bytes to <see cref="OnWrite"/>
/// immediately before they are forwarded so subclasses can count, hash, or cap the
/// flow. Throwing from <see cref="OnWrite"/> prevents the forward — that is how the
/// global size cap is enforced mid-copy.
/// </summary>
internal abstract class WriteOnlyStreamDecorator(Stream inner, bool leaveOpen) : Stream
{
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
        OnWrite(buffer.AsSpan(offset, count));
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        OnWrite(buffer);
        inner.Write(buffer);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        OnWrite(buffer.AsSpan(offset, count));
        await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        OnWrite(buffer.Span);
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override void WriteByte(byte value)
    {
        OnWrite([value]);
        inner.WriteByte(value);
    }

    /// <summary>
    /// Invoked with the bytes about to be forwarded, immediately before the wrapped
    /// stream receives them. Throwing here prevents the forward.
    /// </summary>
    /// <param name="data">The span being written.</param>
    protected abstract void OnWrite(ReadOnlySpan<byte> data);

    /// <summary>
    /// Releases subclass-owned resources. Called at most once, before the wrapped
    /// stream is disposed.
    /// </summary>
    protected virtual void DisposeCore()
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            if (disposing)
            {
                DisposeCore();
                if (!leaveOpen)
                {
                    inner.Dispose();
                }
            }
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            DisposeCore();
            if (!leaveOpen)
            {
                await inner.DisposeAsync().ConfigureAwait(false);
            }
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
