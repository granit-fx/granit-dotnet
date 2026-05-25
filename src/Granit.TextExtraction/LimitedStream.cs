using Granit.TextExtraction.Exceptions;

namespace Granit.TextExtraction;

/// <summary>
/// Read-only stream wrapper that throws <see cref="TextExtractionException"/> with reason
/// <c>input_too_large</c> as soon as the total number of bytes read crosses
/// <see cref="MaxBytes"/>. Mandatory wrapping for every extractor — protects parser libraries
/// from decompression bombs and unbounded uploads (VULN-001).
/// </summary>
/// <remarks>
/// The wrapper does NOT own <see cref="InnerStream"/>; disposing the <see cref="LimitedStream"/>
/// does not dispose the inner stream. This mirrors typical stream-decorator semantics — the
/// caller controls the lifetime of the source.
/// </remarks>
public sealed class LimitedStream : Stream
{
    /// <summary>The wrapped source stream.</summary>
    public Stream InnerStream { get; }

    /// <summary>Hard byte cap. Exceeding it raises <see cref="TextExtractionException"/>.</summary>
    public long MaxBytes { get; }

    /// <summary>Total bytes successfully read so far.</summary>
    public long BytesRead { get; private set; }

    public LimitedStream(Stream innerStream, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(innerStream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (!innerStream.CanRead)
        {
            throw new ArgumentException("LimitedStream requires a readable source.", nameof(innerStream));
        }

        InnerStream = innerStream;
        MaxBytes = maxBytes;
    }

    /// <inheritdoc/>
    public override bool CanRead => InnerStream.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => InnerStream.CanSeek ? InnerStream.Length : throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => BytesRead;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush() { }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        int read = InnerStream.Read(buffer, offset, count);
        Accumulate(read);
        return read;
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        int read = InnerStream.Read(buffer);
        Accumulate(read);
        return read;
    }

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        int read = await InnerStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        Accumulate(read);
        return read;
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await InnerStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Accumulate(read);
        return read;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private void Accumulate(int read)
    {
        if (read <= 0)
        {
            return;
        }

        BytesRead += read;
        if (BytesRead > MaxBytes)
        {
            throw new TextExtractionException("input_too_large");
        }
    }
}
