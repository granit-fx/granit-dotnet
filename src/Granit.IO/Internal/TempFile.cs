using Granit.IO.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.IO.Internal;

/// <summary>
/// Default <see cref="ITempFile"/> implementation.
/// </summary>
internal sealed partial class TempFile : ITempFile
{
    private readonly LimitedStream _stream;
    private readonly IOMetrics _metrics;
    private readonly ILogger _logger;
    private readonly string _category;
    private readonly string? _tenantId;
    private bool _disposed;

    public TempFile(
        string path,
        LimitedStream stream,
        long maxSizeBytes,
        IOMetrics metrics,
        string category,
        string? tenantId,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(logger);

        Path = path;
        _stream = stream;
        MaxSizeBytes = maxSizeBytes;
        _metrics = metrics;
        _category = category;
        _tenantId = tenantId;
        _logger = logger;
    }

    public string Path { get; }

    public Stream Stream => _stream;

    public long MaxSizeBytes { get; }

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
