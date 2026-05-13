using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Exceptions;
using Granit.Browsing.Playwright.Options;
using Granit.Browsing.Sandbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>
/// Hosted service that triggers the Playwright browser install on boot when the
/// <see cref="PlaywrightInstallGuard"/> allows it — equivalent to
/// <c>playwright install</c> from the CLI. Idempotent. The install is bounded by
/// <see cref="PlaywrightOptions.InstallTimeout"/> so a stuck CLI invocation cannot
/// block host startup indefinitely.
/// </summary>
internal sealed partial class PlaywrightProvisionService(
    IOptions<PlaywrightOptions> options,
    IHostEnvironment hostEnvironment,
    ILogger<PlaywrightProvisionService> logger) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        PlaywrightOptions opts = options.Value;
        if (opts.SkipBrowserInstall || !string.IsNullOrEmpty(opts.ExecutablePath))
        {
            if (!string.IsNullOrEmpty(opts.ExecutablePath))
            {
                VerifyExecutableIntegrity(opts);
            }
            return;
        }

        if (!PlaywrightInstallGuard.ShouldAutoInstall(opts, hostEnvironment))
        {
            LogInstallSkipped();
            return;
        }

        using var installCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        installCts.CancelAfter(opts.InstallTimeout);

        LogInstallingBrowsers();
        int exitCode;
        try
        {
            // The Playwright CLI runs synchronously — wrap it in a task we can observe
            // through the bounded cancellation token. The CLI itself doesn't respect the
            // token, but on timeout we surface an actionable error instead of hanging.
            Task<int> installTask = Task.Run(
                () => Microsoft.Playwright.Program.Main(["install", opts.Engine.ToString().ToLowerInvariant()]),
                installCts.Token);

            exitCode = await installTask.WaitAsync(installCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogInstallTimedOut(opts.InstallTimeout);
            throw new TimeoutException(
                $"Playwright `playwright install` exceeded the configured InstallTimeout of {opts.InstallTimeout}.");
        }

        if (exitCode != 0)
        {
            LogInstallFailed(exitCode);
        }
        else
        {
            LogInstallReady();
        }

        if (string.IsNullOrEmpty(opts.ExpectedSha256))
        {
            LogIntegrityPinRecommended();
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void VerifyExecutableIntegrity(PlaywrightOptions opts)
    {
        if (string.IsNullOrEmpty(opts.ExecutablePath))
        {
            return;
        }
        string fullPath = Path.GetFullPath(opts.ExecutablePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Configured Playwright executable not found at '{fullPath}'.");
        }

        if (!string.IsNullOrEmpty(opts.ExpectedSha256))
        {
            using FileStream stream = File.OpenRead(fullPath);
            byte[] hash = SHA256.HashData(stream);
            string actual = Convert.ToHexString(hash);
            string expected = opts.ExpectedSha256.Replace("-", string.Empty, StringComparison.Ordinal);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new SandboxViolationException(
                    SandboxViolationKind.ExecutablePathRejected,
                    $"Playwright executable '{fullPath}' SHA-256 digest does not match the configured ExpectedSha256 pin (expected={expected}, actual={actual}).");
            }
        }
        else
        {
            LogIntegrityPinRecommended();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.Playwright running browser install (`playwright install`)...")]
    private partial void LogInstallingBrowsers();

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.Playwright browser install ready.")]
    private partial void LogInstallReady();

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.Playwright auto-install disabled (production default or explicit opt-out); skipping `playwright install`.")]
    private partial void LogInstallSkipped();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Granit.Browsing.Playwright `playwright install` exited with code {ExitCode}.")]
    private partial void LogInstallFailed(int exitCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Granit.Browsing.Playwright `playwright install` exceeded the configured InstallTimeout of {Timeout} and was aborted.")]
    private partial void LogInstallTimedOut(TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.Playwright browser executable integrity is not pinned (PlaywrightOptions.ExpectedSha256). Without a pin, integrity relies on upstream HTTPS only.")]
    private partial void LogIntegrityPinRecommended();
}
