using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Playwright.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>
/// Hosted service that triggers the Playwright browser install on boot when not skipped
/// — equivalent to <c>playwright install</c> from the CLI. Idempotent.
/// </summary>
internal sealed partial class PlaywrightProvisionService(
    IOptions<PlaywrightOptions> options,
    ILogger<PlaywrightProvisionService> logger) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        PlaywrightOptions opts = options.Value;
        if (opts.SkipBrowserInstall || !string.IsNullOrEmpty(opts.ExecutablePath))
        {
            return Task.CompletedTask;
        }

        LogInstallingBrowsers();
        // Microsoft.Playwright bundles a CLI entry point in the assembly; invoking it
        // with ["install"] downloads the browser binaries to the framework cache.
        int exitCode = Microsoft.Playwright.Program.Main(["install", opts.Engine.ToString().ToLowerInvariant()]);
        if (exitCode != 0)
        {
            LogInstallFailed(exitCode);
        }
        else
        {
            LogInstallReady();
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.Playwright running browser install (`playwright install`)...")]
    private partial void LogInstallingBrowsers();

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.Playwright browser install ready.")]
    private partial void LogInstallReady();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Granit.Browsing.Playwright `playwright install` exited with code {ExitCode}.")]
    private partial void LogInstallFailed(int exitCode);
}
