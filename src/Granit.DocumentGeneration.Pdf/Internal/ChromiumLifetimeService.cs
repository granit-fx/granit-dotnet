using Granit.DocumentGeneration.Pdf.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace Granit.DocumentGeneration.Pdf.Internal;

/// <summary>
/// Manages the lifecycle of the headless Chromium browser instance.
/// Starts the browser on application startup and disposes it on shutdown.
/// </summary>
internal sealed partial class ChromiumLifetimeService(
    IOptions<PdfRenderOptions> options,
    ILogger<ChromiumLifetimeService> logger) : IHostedService, IAsyncDisposable
{
    private IBrowser? _browser;
    private readonly SemaphoreSlim _pageSemaphore = new(options.Value.MaxConcurrentPages);

    /// <summary>
    /// Gets the browser instance. Throws if the service has not been started.
    /// </summary>
    internal IBrowser Browser => _browser ?? throw new InvalidOperationException(
        "Chromium browser is not available. Ensure ChromiumLifetimeService has been started.");

    /// <summary>
    /// Gets the semaphore that limits concurrent page usage.
    /// </summary>
    internal SemaphoreSlim PageSemaphore => _pageSemaphore;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        PdfRenderOptions opts = options.Value;

        if (string.IsNullOrEmpty(opts.ChromiumExecutablePath))
        {
            LogDownloadingChromium();
            BrowserFetcher fetcher = new();
            await fetcher.DownloadAsync().ConfigureAwait(false);
        }

        List<string> chromiumArgs =
        [
            "--disable-dev-shm-usage",
            "--disable-gpu",
            "--disable-extensions",
            "--disable-background-networking",
        ];

        if (opts.DisableSandbox)
        {
            LogSandboxDisabled();
            chromiumArgs.AddRange(["--no-sandbox", "--disable-setuid-sandbox"]);
        }

        LaunchOptions launchOptions = new()
        {
            Headless = true,
            Args = [.. chromiumArgs],
        };

        if (!string.IsNullOrEmpty(opts.ChromiumExecutablePath))
        {
            string fullPath = Path.GetFullPath(opts.ChromiumExecutablePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Configured Chromium executable not found at '{fullPath}'.");
            }

            launchOptions.ExecutablePath = fullPath;
        }

        LogStartingChromium();
        _browser = await Puppeteer.LaunchAsync(launchOptions).ConfigureAwait(false);
        LogChromiumStarted();
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_browser is not null)
        {
            LogShuttingDownChromium();
            await _browser.DisposeAsync().ConfigureAwait(false);
            _browser = null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
            _browser = null;
        }

        _pageSemaphore.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading Chromium browser for PDF rendering...")]
    private partial void LogDownloadingChromium();

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting headless Chromium for PDF rendering...")]
    private partial void LogStartingChromium();

    [LoggerMessage(Level = LogLevel.Information, Message = "Headless Chromium started successfully")]
    private partial void LogChromiumStarted();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chromium sandbox is disabled via configuration. " +
        "This reduces process isolation and should only be used in containerized environments")]
    private partial void LogSandboxDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutting down headless Chromium...")]
    private partial void LogShuttingDownChromium();
}
