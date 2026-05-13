using System;
using System.Threading.Tasks;
using Granit.Browsing.PuppeteerSharp.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// Gates <c>BrowserFetcher.DownloadAsync</c>: refuses the network download in
/// production unless a host explicitly opts-in (<c>SkipChromiumDownload = true</c> with
/// <c>ChromiumExecutablePath</c> set), and logs a structured warning whenever the
/// download path is exercised.
/// </summary>
internal sealed partial class PuppeteerBrowserFetcherIntegrity(
    IHostEnvironment hostEnvironment,
    ILogger<PuppeteerBrowserFetcherIntegrity> logger)
{
    /// <summary>
    /// Downloads the bundled Chromium build through <see cref="BrowserFetcher"/>, after
    /// asserting production safety.
    /// </summary>
    public async Task DownloadAsync(PuppeteerSharpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (hostEnvironment.IsProduction()
            && string.IsNullOrEmpty(options.ChromiumExecutablePath)
            && !options.SkipChromiumDownload)
        {
            throw new InvalidOperationException(
                "Granit.Browsing.PuppeteerSharp refuses to download Chromium in production. "
                + "Set Browsing:PuppeteerSharp:SkipChromiumDownload=true and pin "
                + "Browsing:PuppeteerSharp:ChromiumExecutablePath to a vetted binary.");
        }

        LogDownloadStart(hostEnvironment.EnvironmentName);
        BrowserFetcher fetcher = new();
        await fetcher.DownloadAsync().ConfigureAwait(false);
        LogDownloadComplete();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Granit.Browsing.PuppeteerSharp is downloading a Chromium build at runtime (environment={Environment}). Pin a verified executable in production.")]
    private partial void LogDownloadStart(string environment);

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.PuppeteerSharp Chromium build ready.")]
    private partial void LogDownloadComplete();
}
