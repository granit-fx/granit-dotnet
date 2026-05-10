using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.PuppeteerSharp.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// Hosted service that downloads the bundled Chromium build on the first start when no
/// executable path is configured. Idempotent — subsequent boots find the binary on
/// disk and skip the download.
/// </summary>
internal sealed partial class PuppeteerChromiumProvisionService(
    IOptions<PuppeteerSharpOptions> options,
    ILogger<PuppeteerChromiumProvisionService> logger) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        PuppeteerSharpOptions opts = options.Value;

        if (opts.SkipChromiumDownload)
        {
            return;
        }

        if (!string.IsNullOrEmpty(opts.ChromiumExecutablePath))
        {
            string fullPath = Path.GetFullPath(opts.ChromiumExecutablePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Configured Chromium executable not found at '{fullPath}'.");
            }
            return;
        }

        LogDownloadingChromium();
        BrowserFetcher fetcher = new();
        await fetcher.DownloadAsync().ConfigureAwait(false);
        LogChromiumReady();
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.PuppeteerSharp downloading the bundled Chromium build...")]
    private partial void LogDownloadingChromium();

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.PuppeteerSharp Chromium build ready.")]
    private partial void LogChromiumReady();
}
