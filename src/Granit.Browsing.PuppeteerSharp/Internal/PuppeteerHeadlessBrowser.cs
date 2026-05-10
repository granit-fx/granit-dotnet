using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Options;
using Granit.Browsing.PuppeteerSharp.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using GranitBrowsingOptions = Granit.Browsing.Options.GranitBrowsingOptions;
using IPuppeteerBrowser = PuppeteerSharp.IBrowser;
using IPuppeteerPage = PuppeteerSharp.IPage;
using PuppeteerLaunchOptions = PuppeteerSharp.LaunchOptions;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// Chromium-backed <see cref="IHeadlessBrowser"/> implementation built on PuppeteerSharp.
/// Owns a single persistent browser instance plus a pool of pages capped by
/// <c>GranitBrowsingOptions.MaxBrowsers × GranitBrowsingOptions.MaxPagesPerBrowser</c>.
/// </summary>
/// <remarks>
/// <para>
/// Phase A keeps the topology simple: one browser process, multiple pages multiplexed on
/// it through a semaphore. Phase B (Playwright provider) and a future Phase B follow-up
/// can lift the multi-browser pool when memory pressure / parallelism demands it; the
/// public surface stays unchanged.
/// </para>
/// </remarks>
internal sealed partial class PuppeteerHeadlessBrowser(
    IOptions<GranitBrowsingOptions> browsingOptions,
    IOptions<PuppeteerSharpOptions> puppeteerOptions,
    BrowsingMetrics metrics,
    ILogger<PuppeteerHeadlessBrowser> logger,
    ICurrentTenant? currentTenant = null,
    IBrowserSandboxProfile? sandbox = null)
    : IHeadlessBrowser, IHeadlessBrowserPool, IAsyncDisposable
{
    private const string Engine = "chromium-puppeteer";

    private string? CurrentTenantId =>
        currentTenant is { IsAvailable: true } t ? t.Id?.ToString() : null;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _pageSemaphore = new(
        browsingOptions.Value.MaxBrowsers * browsingOptions.Value.MaxPagesPerBrowser,
        browsingOptions.Value.MaxBrowsers * browsingOptions.Value.MaxPagesPerBrowser);
    private IPuppeteerBrowser? _browser;
    private int _activePages;
    private int _waitingAcquisitions;
    private long _totalAcquisitions;
    private bool _drained;

    /// <inheritdoc/>
    public string EngineName => Engine;

    /// <inheritdoc/>
    public BrowserCapabilities Capabilities =>
        BrowserCapabilities.Screenshot
        | BrowserCapabilities.PdfGeneration
        | BrowserCapabilities.PdfViewerNative
        | BrowserCapabilities.NetworkInterception
        | BrowserCapabilities.JavaScriptInjection
        | BrowserCapabilities.EmulateDevice
        | BrowserCapabilities.EmulateMedia
        | BrowserCapabilities.AccessibilityTree;

    /// <inheritdoc/>
    public bool Supports(BrowserCapabilities capability) =>
        (Capabilities & capability) == capability;

    /// <inheritdoc/>
    public int ActiveBrowsers => _browser is null ? 0 : 1;

    /// <inheritdoc/>
    public int IdlePages => _pageSemaphore.CurrentCount;

    /// <inheritdoc/>
    public Task<PoolStats> GetStatsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new PoolStats(
            ActiveBrowsers: ActiveBrowsers,
            ActivePages: _activePages,
            IdlePages: _pageSemaphore.CurrentCount,
            WaitingAcquisitions: _waitingAcquisitions,
            TotalAcquisitions: Interlocked.Read(ref _totalAcquisitions)));

    /// <inheritdoc/>
    public async Task<IBrowserPage> AcquirePageAsync(
        BrowserPageOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_drained)
        {
            throw new InvalidOperationException("The Granit.Browsing pool has been drained — no new pages can be acquired.");
        }

        using Activity? activity = BrowsingActivitySource.Source.StartActivity(BrowsingActivitySource.PageAcquire);
        var stopwatch = Stopwatch.StartNew();

        Interlocked.Increment(ref _waitingAcquisitions);
        try
        {
            using var acquireCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            acquireCts.CancelAfter(browsingOptions.Value.AcquireTimeout);

            try
            {
                await _pageSemaphore.WaitAsync(acquireCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                metrics.RecordError(Engine, CurrentTenantId, "acquire_timeout");
                throw new TimeoutException(
                    $"Acquiring a Granit.Browsing page exceeded {browsingOptions.Value.AcquireTimeout}.");
            }
        }
        finally
        {
            Interlocked.Decrement(ref _waitingAcquisitions);
        }

        try
        {
            await EnsureBrowserStartedAsync(cancellationToken).ConfigureAwait(false);

            IPuppeteerPage puppeteerPage = await _browser!.NewPageAsync().ConfigureAwait(false);
            await ApplyPageOptionsAsync(puppeteerPage, options, cancellationToken).ConfigureAwait(false);
            await ApplySandboxAsync(puppeteerPage, cancellationToken).ConfigureAwait(false);

            Interlocked.Increment(ref _activePages);
            Interlocked.Increment(ref _totalAcquisitions);
            string? tenantId = CurrentTenantId;
            metrics.RecordPageAcquired(Engine, tenantId);
            metrics.RecordAcquireDuration(Engine, tenantId, stopwatch.Elapsed);

            return new PuppeteerBrowserPage(puppeteerPage, OnPageReleased);
        }
        catch
        {
            _pageSemaphore.Release();
            throw;
        }
    }

    private void OnPageReleased()
    {
        Interlocked.Decrement(ref _activePages);
        _pageSemaphore.Release();
        metrics.RecordPageReleased(Engine, CurrentTenantId);
    }

    private async Task EnsureBrowserStartedAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsClosed: false, IsConnected: true })
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_browser is { IsClosed: false, IsConnected: true })
            {
                return;
            }
            if (_browser is not null)
            {
                // Crashed Chromium — drop the dead handle and re-launch.
                try { await _browser.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
                _browser = null;
            }

            PuppeteerSharpOptions opts = puppeteerOptions.Value;
            List<string> args =
            [
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-extensions",
                "--disable-background-networking",
            ];
            if (opts.DisableSandbox)
            {
                args.AddRange(["--no-sandbox", "--disable-setuid-sandbox"]);
                LogSandboxDisabled();
            }
            args.AddRange(opts.ExtraArgs);

            PuppeteerLaunchOptions launch = new()
            {
                Headless = true,
                Args = [.. args],
            };
            if (!string.IsNullOrEmpty(opts.ChromiumExecutablePath))
            {
                launch.ExecutablePath = System.IO.Path.GetFullPath(opts.ChromiumExecutablePath);
            }

            LogStartingChromium();
            _browser = await Puppeteer.LaunchAsync(launch).ConfigureAwait(false);
            LogChromiumStarted();
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task ApplyPageOptionsAsync(
        IPuppeteerPage page,
        BrowserPageOptions? options,
        CancellationToken cancellationToken)
    {
        if (options is null)
        {
            return;
        }

        if (options.Viewport is { } vp)
        {
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = vp.Width,
                Height = vp.Height,
                DeviceScaleFactor = options.DeviceScaleFactor,
            }).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(options.UserAgent))
        {
            await page.SetUserAgentAsync(options.UserAgent).ConfigureAwait(false);
        }

        if (options.ExtraHeaders is { Count: > 0 } headers)
        {
            Dictionary<string, string> dict = new(headers);
            await page.SetExtraHttpHeadersAsync(dict).ConfigureAwait(false);
        }

        if (!options.JavaScriptEnabled)
        {
            await page.SetJavaScriptEnabledAsync(false).ConfigureAwait(false);
        }

        if (options.BypassCsp)
        {
            await page.SetBypassCSPAsync(true).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(options.MediaType))
        {
            await page.EmulateMediaTypeAsync(options.MediaType.Equals("print", StringComparison.OrdinalIgnoreCase)
                ? global::PuppeteerSharp.Media.MediaType.Print
                : global::PuppeteerSharp.Media.MediaType.Screen).ConfigureAwait(false);
        }

        _ = cancellationToken;
    }

    private async Task ApplySandboxAsync(IPuppeteerPage page, CancellationToken cancellationToken)
    {
        if (sandbox is null)
        {
            return;
        }

        if (sandbox.DisableJavaScript)
        {
            await page.SetJavaScriptEnabledAsync(false).ConfigureAwait(false);
        }

        if (sandbox.BlockNetworkRequests || sandbox.DisableImages || (sandbox.BlockedUrlPatterns?.Count ?? 0) > 0)
        {
            await page.SetRequestInterceptionAsync(true).ConfigureAwait(false);
            page.Request += async (_, e) =>
            {
                if (sandbox.BlockNetworkRequests)
                {
                    await e.Request.AbortAsync().ConfigureAwait(false);
                    return;
                }
                if (sandbox.DisableImages && e.Request.ResourceType == ResourceType.Image)
                {
                    await e.Request.AbortAsync().ConfigureAwait(false);
                    return;
                }
                if (sandbox.BlockedUrlPatterns is { Count: > 0 } patterns)
                {
                    foreach (string pattern in patterns)
                    {
                        if (e.Request.Url.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            await e.Request.AbortAsync().ConfigureAwait(false);
                            return;
                        }
                    }
                }
                await e.Request.ContinueAsync().ConfigureAwait(false);
            };
        }

        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        _drained = true;
        // Wait for in-flight pages to complete by acquiring all permits.
        int total = browsingOptions.Value.MaxBrowsers * browsingOptions.Value.MaxPagesPerBrowser;
        for (int i = 0; i < total; i++)
        {
            await _pageSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_browser is not null)
        {
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
        _initLock.Dispose();
    }

    /// <summary>Internal access to the underlying browser for capability impls.</summary>
    internal IPuppeteerBrowser RequireBrowser() =>
        _browser ?? throw new InvalidOperationException(
            "PuppeteerSharp browser not started — acquire a page first or call EnsureBrowserStartedAsync.");

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.PuppeteerSharp launching headless Chromium...")]
    private partial void LogStartingChromium();

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.PuppeteerSharp headless Chromium launched.")]
    private partial void LogChromiumStarted();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Granit.Browsing.PuppeteerSharp launching with the Chromium sandbox disabled — only acceptable in containerised environments.")]
    private partial void LogSandboxDisabled();
}
