using Granit.IO.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.IO.Internal;

/// <summary>
/// Default <see cref="ITempFile"/> implementation.
/// </summary>
internal sealed partial class TempFile(
    string path,
    LimitedStream stream,
    long maxSizeBytes,
    IoMetrics metrics,
    string category,
    string? tenantId,
    ILogger logger) : ITempFile
{
    private readonly LimitedStream _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    private readonly IoMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _category = category ?? throw new ArgumentNullException(nameof(category));
    private readonly string? _tenantId = tenantId;
    private bool _disposed;

    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    public Stream Stream => _stream;

    public long MaxSizeBytes { get; } = maxSizeBytes;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        long finalSize = 0;
        try
        {
            finalSize = _stream.CanRead ? _stream.Length : 0;
        }
        catch (ObjectDisposedException)
        {
            // already gone — best-effort
        }

        // DeleteOnClose semantics: closing the FileStream removes the file from disk.
        await _stream.DisposeAsync().ConfigureAwait(false);

        // Defensive belt: if the file persists (Windows DeleteOnClose race, shared
        // handle held by AV/indexer), try once more.
        try
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
        catch (IOException ex)
        {
            LogResidualDeleteFailed(_logger, Path, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogResidualDeleteFailed(_logger, Path, ex);
        }

        _metrics.RecordDeleted(_category, _tenantId);
        _metrics.RecordBytes(_category, _tenantId, finalSize);
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Residual temp file delete failed for '{Path}'.")]
    private static partial void LogResidualDeleteFailed(ILogger logger, string path, Exception exception);
}
