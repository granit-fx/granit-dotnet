namespace Granit.Privacy.BlobStorage.DataExport.Exceptions;

/// <summary>
/// Raised by the streaming archive assembler when a privacy export write exceeds the
/// configured cap (<c>GranitPrivacyOptions.ExportMaxSizeMb</c>). Caught by the assembler
/// to transition the request to <c>ExportRequestState.SizeLimitExceeded</c>.
/// </summary>
public sealed class PrivacyExportSizeLimitExceededException(long maxBytes, long observedBytes)
    : Exception($"Export archive exceeded the configured size limit of {maxBytes} bytes (observed {observedBytes}).")
{
    public long MaxBytes { get; } = maxBytes;

    public long ObservedBytes { get; } = observedBytes;
}
