using Granit.Privacy.BlobStorage.DataExport.Exceptions;

namespace Granit.Privacy.BlobStorage.DataExport.Internal;

/// <summary>
/// Write-only decorator stream that tracks how many bytes have been written and
/// short-circuits with <see cref="PrivacyExportSizeLimitExceededException"/> the instant
/// the configured cap is crossed. Wrapping the ZIP output stream lets the archive
/// assembler enforce <c>GranitPrivacyOptions.ExportMaxSizeMb</c> during the stream copy —
/// a post-hoc check would require buffering the whole archive first, which defeats the
/// purpose of the size limit.
/// </summary>
internal sealed class CountingStream(Stream inner, long maxBytes)
    : WriteOnlyStreamDecorator(inner, leaveOpen: false)
{
    public long BytesWritten { get; private set; }

    protected override void OnWrite(ReadOnlySpan<byte> data)
    {
        BytesWritten += data.Length;
        if (BytesWritten > maxBytes)
        {
            throw new PrivacyExportSizeLimitExceededException(maxBytes, BytesWritten);
        }
    }
}
