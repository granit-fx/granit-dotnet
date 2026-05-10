using Granit.IO.Diagnostics;
using Granit.IO.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.IO.Internal;

/// <summary>
/// Background service that purges temp files whose <c>LastWriteTimeUtc</c> is older
/// than <see cref="TempFileOptions.MaxLifetime"/>.
/// </summary>
internal sealed partial class TempFileJanitor : BackgroundService
{
    private readonly TempFileOptions _options;
    private readonly IoMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TempFileJanitor> _logger;
    private readonly string _rootDirectory;

    public TempFileJanitor(
        IOptions<TempFileOptions> options,
        IoMetrics metrics,
        TimeProvider timeProvider,
        ILogger<TempFileJanitor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rootDirectory = string.IsNullOrWhiteSpace(_options.RootDirectory)
            ? Path.Combine(Path.GetTempPath(), "granit")
            : _options.RootDirectory!;
    }

    /// <summary>Root directory swept by the janitor.</summary>
    public string RootDirectory => _rootDirectory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.RunJanitor)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RunOnce();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTickFailed(_logger, ex);
            }

            try
            {
                await Task.Delay(_options.JanitorInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Executes a single janitor sweep. Public for tests; not called by hosting after startup.
    /// </summary>
    public void RunOnce()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return;
        }

        DateTime cutoffUtc = _timeProvider.GetUtcNow().UtcDateTime - _options.MaxLifetime;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(_rootDirectory, "*", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogEnumerationFailed(_logger, _rootDirectory, ex);
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }

        foreach (string file in files)
        {
            try
            {
                DateTime lastWrite = File.GetLastWriteTimeUtc(file);
                if (lastWrite < cutoffUtc)
                {
                    File.Delete(file);
                    _metrics.RecordJanitorPurged("age");
                    LogPurged(_logger, file);
                }
            }
            catch (FileNotFoundException)
            {
                // already gone
            }
            catch (IOException ex)
            {
                LogPurgeFailed(_logger, file, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogPurgeFailed(_logger, file, ex);
            }
        }
    }

    [LoggerMessage(EventId = 2020, Level = LogLevel.Debug, Message = "Janitor purged temp file '{Path}'.")]
    private static partial void LogPurged(ILogger logger, string path);

    [LoggerMessage(EventId = 2021, Level = LogLevel.Debug, Message = "Janitor failed to purge '{Path}'.")]
    private static partial void LogPurgeFailed(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 2022, Level = LogLevel.Warning, Message = "Janitor enumeration failed for '{Root}'.")]
    private static partial void LogEnumerationFailed(ILogger logger, string root, Exception exception);

    [LoggerMessage(EventId = 2023, Level = LogLevel.Warning, Message = "Janitor tick failed.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
