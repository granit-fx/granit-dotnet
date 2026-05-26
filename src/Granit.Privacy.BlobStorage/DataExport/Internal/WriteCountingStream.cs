namespace Granit.Privacy.BlobStorage.DataExport.Internal;

/// <summary>
/// Forward-only write-counter decorator. Tracks how many bytes the wrapped
/// stream has accepted without imposing a cap — used by <c>ShardingArchiveWriter</c>
/// to decide when to roll over to a new shard.
/// </summary>
/// <remarks>
/// <see cref="CountingStream"/> short-circuits with
/// <c>PrivacyExportSizeLimitExceededException</c> on the global cap; this sibling
/// only observes. Both flavours co-exist because the sharding writer's per-shard
/// counter is a routing signal, not a defensive limit.
/// </remarks>
internal sealed class WriteCountingStream(Stream inner, bool leaveOpen) : Stream
{
    public long BytesWritten { get; private set; }

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
        BytesWritten += count;
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        BytesWritten += buffer.Length;
        inner.Write(buffer);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        BytesWritten += count;
        await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        BytesWritten += buffer.Length;
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override void WriteByte(byte value)
    {
        BytesWritten += 1;
        inner.WriteByte(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!leaveOpen)
        {
            await inner.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
