using System.Security.Cryptography;
using Granit.Browsing.Exceptions;
using Granit.Browsing.PuppeteerSharp.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// Decides whether <c>BrowserFetcher.DownloadAsync</c> may run: refuses the network
/// download in production unless a host explicitly opts-in (<c>SkipChromiumDownload = true</c>
/// with <c>ChromiumExecutablePath</c> set), and logs a structured warning whenever the
/// download path is exercised. Without an
/// <see cref="PuppeteerSharpOptions.ExpectedSha256"/> pin, integrity relies on the
/// upstream HTTPS download — operators are warned at startup.
/// </summary>
internal sealed partial class PuppeteerBrowserFetcherPolicy(
    IHostEnvironment hostEnvironment,
    ILogger<PuppeteerBrowserFetcherPolicy> logger)
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

    /// <summary>
    /// Verifies the SHA-256 digest of <paramref name="executablePath"/> against the
    /// configured <see cref="PuppeteerSharpOptions.ExpectedSha256"/>. No-op when the
    /// pin is unset; an Information-level message is emitted once at startup recommending
    /// the pin (handled by the caller).
    /// </summary>
    /// <exception cref="SandboxViolationException">
    /// When the executable digest does not match the configured expected value.
    /// </exception>
    public static async Task VerifyIntegrityAsync(string executablePath, string? expectedSha256)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        if (string.IsNullOrEmpty(expectedSha256))
        {
            return;
        }

        await using FileStream stream = File.OpenRead(executablePath);
        byte[] hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        string actual = Convert.ToHexString(hash);

        string expected = expectedSha256.Replace("-", string.Empty, StringComparison.Ordinal);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new SandboxViolationException(
                SandboxViolationKind.ExecutablePathRejected,
                $"Chromium executable '{executablePath}' SHA-256 digest does not match the configured ExpectedSha256 pin (expected={expected}, actual={actual}).");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Granit.Browsing.PuppeteerSharp is downloading a Chromium build at runtime (environment={Environment}). Pin a verified executable in production.")]
    private partial void LogDownloadStart(string environment);

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.PuppeteerSharp Chromium build ready.")]
    private partial void LogDownloadComplete();
}
