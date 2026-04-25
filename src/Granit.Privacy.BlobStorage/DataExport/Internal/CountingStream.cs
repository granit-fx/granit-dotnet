namespace Granit.Privacy.BlobStorage.DataExport.Internal;

/// <summary>
/// Write-only decorator stream that tracks how many bytes have been written and
/// short-circuits with <see cref="PrivacyExportSizeLimitExceededException"/> the instant
/// the configured cap is crossed. Wrapping the ZIP output stream lets the archive
/// assembler enforce <c>GranitPrivacyOptions.ExportMaxSizeMb</c> during the stream copy —
/// a post-hoc check would require buffering the whole archive first, which defeats the
/// purpose of the size limit.
/// </summary>
internal sealed class CountingStream(Stream inner, long maxBytes) : Stream
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
        Track(count);
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Track(buffer.Length);
        inner.Write(buffer);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Track(count);
        await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Track(buffer.Length);
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override void WriteByte(byte value)
    {
        Track(1);
        inner.WriteByte(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private void Track(int count)
    {
        BytesWritten += count;
        if (BytesWritten > maxBytes)
        {
            throw new PrivacyExportSizeLimitExceededException(maxBytes, BytesWritten);
        }
    }
}

/// <summary>
/// Raised by <see cref="CountingStream"/> when the archive write exceeds the configured cap.
/// Caught by the archive assembler to transition the request to
/// <see cref="Granit.Privacy.DataExport.ExportRequestState.SizeLimitExceeded"/>.
/// </summary>
public sealed class PrivacyExportSizeLimitExceededException(long maxBytes, long observedBytes)
    : Exception($"Export archive exceeded the configured size limit of {maxBytes} bytes (observed {observedBytes}).")
{
    public long MaxBytes { get; } = maxBytes;

    public long ObservedBytes { get; } = observedBytes;
}
