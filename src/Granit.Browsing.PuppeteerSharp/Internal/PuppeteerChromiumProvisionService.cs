using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.PuppeteerSharp.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// Hosted service that downloads the bundled Chromium build on the first start when no
/// executable path is configured. Idempotent — subsequent boots find the binary on
/// disk and skip the download. Production safety is enforced by
/// <see cref="PuppeteerBrowserFetcherIntegrity"/>.
/// </summary>
internal sealed partial class PuppeteerChromiumProvisionService(
    IOptions<PuppeteerSharpOptions> options,
    PuppeteerBrowserFetcherIntegrity integrity,
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

        await integrity.DownloadAsync(opts).ConfigureAwait(false);
        LogChromiumReady();
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.PuppeteerSharp Chromium build ready.")]
    private partial void LogChromiumReady();
}
