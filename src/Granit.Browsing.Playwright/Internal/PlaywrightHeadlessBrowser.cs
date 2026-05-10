using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Options;
using Granit.Browsing.Playwright.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>
/// Microsoft.Playwright-backed <see cref="IHeadlessBrowser"/>. Capabilities advertised
/// depend on <see cref="PlaywrightOptions.Engine"/>: PDF generation and the native PDF
/// viewer are advertised on Chromium only; tracing and HAR recording are advertised on
/// every engine.
/// </summary>
internal sealed partial class PlaywrightHeadlessBrowser : IHeadlessBrowser, IHeadlessBrowserPool, IAsyncDisposable
{
    private readonly IOptions<GranitBrowsingOptions> _browsingOptions;
    private readonly IOptions<PlaywrightOptions> _playwrightOptions;
    private readonly BrowsingMetrics _metrics;
    private readonly ILogger<PlaywrightHeadlessBrowser> _logger;
    private readonly ICurrentTenant? _currentTenant;
    private readonly IBrowserSandboxProfile? _sandbox;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _pageSemaphore;
    private readonly string _engineName;
    private readonly BrowserCapabilities _capabilities;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private int _activePages;
    private int _waitingAcquisitions;
    private long _totalAcquisitions;
    private bool _drained;

    public PlaywrightHeadlessBrowser(
        IOptions<GranitBrowsingOptions> browsingOptions,
        IOptions<PlaywrightOptions> playwrightOptions,
        BrowsingMetrics metrics,
        ILogger<PlaywrightHeadlessBrowser> logger,
        ICurrentTenant? currentTenant = null,
        IBrowserSandboxProfile? sandbox = null)
    {
        _browsingOptions = browsingOptions;
        _playwrightOptions = playwrightOptions;
        _metrics = metrics;
        _logger = logger;
        _currentTenant = currentTenant;
        _sandbox = sandbox;

        int permits = _browsingOptions.Value.MaxBrowsers * _browsingOptions.Value.MaxPagesPerBrowser;
        _pageSemaphore = new SemaphoreSlim(permits, permits);

        _engineName = _playwrightOptions.Value.Engine switch
        {
            BrowserEngine.Chromium => "chromium-playwright",
            BrowserEngine.Firefox => "firefox-playwright",
            BrowserEngine.Webkit => "webkit-playwright",
            _ => throw new InvalidOperationException(
                $"Unsupported Playwright engine: {_playwrightOptions.Value.Engine}."),
        };

        // AccessibilityTree intentionally not advertised — Playwright .NET deprecated
        // its IAccessibility tree-export API in favour of ARIA snapshots, which don't
        // map cleanly to the AccessibilityTreeNode shape.
        BrowserCapabilities caps =
            BrowserCapabilities.Screenshot
            | BrowserCapabilities.NetworkInterception
            | BrowserCapabilities.JavaScriptInjection
            | BrowserCapabilities.EmulateDevice
            | BrowserCapabilities.EmulateMedia
            | BrowserCapabilities.HarRecording
            | BrowserCapabilities.TraceRecording;
        if (_playwrightOptions.Value.Engine == BrowserEngine.Chromium)
        {
            caps |= BrowserCapabilities.PdfGeneration | BrowserCapabilities.PdfViewerNative;
        }
        _capabilities = caps;
    }

    private string? CurrentTenantId =>
        _currentTenant is { IsAvailable: true } t ? t.Id?.ToString() : null;

    /// <inheritdoc/>
    public string EngineName => _engineName;

    /// <inheritdoc/>
    public BrowserCapabilities Capabilities => _capabilities;

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
        var sw = Stopwatch.StartNew();

        Interlocked.Increment(ref _waitingAcquisitions);
        try
        {
            using var acquireCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            acquireCts.CancelAfter(_browsingOptions.Value.AcquireTimeout);
            try
            {
                await _pageSemaphore.WaitAsync(acquireCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _metrics.RecordError(EngineName, CurrentTenantId, "acquire_timeout");
                throw new TimeoutException(
                    $"Acquiring a Granit.Browsing page exceeded {_browsingOptions.Value.AcquireTimeout}.");
            }
        }
        finally
        {
            Interlocked.Decrement(ref _waitingAcquisitions);
        }

        try
        {
            await EnsureBrowserStartedAsync(cancellationToken).ConfigureAwait(false);

            var contextOptions = new BrowserNewContextOptions();
            if (options?.Viewport is { } vp)
            {
                contextOptions.ViewportSize = new Microsoft.Playwright.ViewportSize { Width = vp.Width, Height = vp.Height };
            }
            contextOptions.DeviceScaleFactor = (float?)options?.DeviceScaleFactor;
            contextOptions.UserAgent = options?.UserAgent;
            contextOptions.Locale = options?.Locale;
            contextOptions.TimezoneId = options?.TimezoneId;
            // Sandbox profile takes precedence over per-call BrowserPageOptions for JS enablement.
            bool jsEnabled = options?.JavaScriptEnabled ?? true;
            if (_sandbox?.DisableJavaScript == true)
            {
                jsEnabled = false;
            }
            contextOptions.JavaScriptEnabled = jsEnabled;
            contextOptions.BypassCSP = options?.BypassCsp ?? false;
            contextOptions.ColorScheme = options?.ColorScheme switch
            {
                "light" => ColorScheme.Light,
                "dark" => ColorScheme.Dark,
                "no-preference" => ColorScheme.NoPreference,
                _ => null,
            };
            if (options?.ExtraHeaders is { Count: > 0 } headers)
            {
                Dictionary<string, string> dict = new(headers);
                contextOptions.ExtraHTTPHeaders = dict;
            }

            IBrowserContext context = await _browser!.NewContextAsync(contextOptions).ConfigureAwait(false);

            if (options?.Cookies is { Count: > 0 } cookies)
            {
                List<Cookie> mapped = new(cookies.Count);
                foreach (CookieParam c in cookies)
                {
                    Cookie cookie = new() { Name = c.Name, Value = c.Value, Domain = c.Domain, Path = c.Path, Url = c.Url };
                    if (c.Expires is { } exp)
                    {
                        cookie.Expires = exp;
                    }
                    cookie.HttpOnly = c.HttpOnly;
                    cookie.Secure = c.Secure;
                    cookie.SameSite = c.SameSite switch
                    {
                        "Strict" => SameSiteAttribute.Strict,
                        "Lax" => SameSiteAttribute.Lax,
                        "None" => SameSiteAttribute.None,
                        _ => null,
                    };
                    mapped.Add(cookie);
                }
                await context.AddCookiesAsync(mapped).ConfigureAwait(false);
            }

            IPage playwrightPage = await context.NewPageAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(options?.MediaType))
            {
                await playwrightPage.EmulateMediaAsync(new PageEmulateMediaOptions
                {
                    Media = options.MediaType.Equals("print", StringComparison.OrdinalIgnoreCase)
                        ? Media.Print
                        : Media.Screen,
                }).ConfigureAwait(false);
            }

            await ApplySandboxAsync(playwrightPage).ConfigureAwait(false);

            Interlocked.Increment(ref _activePages);
            Interlocked.Increment(ref _totalAcquisitions);
            string? tenantId = CurrentTenantId;
            _metrics.RecordPageAcquired(EngineName, tenantId);
            _metrics.RecordAcquireDuration(EngineName, tenantId, sw.Elapsed);

            return new PlaywrightBrowserPage(playwrightPage, context, EngineName, OnPageReleased);
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
        _metrics.RecordPageReleased(EngineName, CurrentTenantId);
    }

    private async Task EnsureBrowserStartedAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true })
        {
            return;
        }
        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_browser is { IsConnected: true })
            {
                return;
            }
            if (_browser is not null)
            {
                // Crashed engine — drop the dead handle and re-launch.
                try { await _browser.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
                _browser = null;
            }

            PlaywrightOptions opts = _playwrightOptions.Value;
            _playwright ??= await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);

            BrowserTypeLaunchOptions launch = new()
            {
                Headless = true,
                ExecutablePath = opts.ExecutablePath,
                Args = opts.ExtraArgs,
            };

            IBrowserType type = opts.Engine switch
            {
                BrowserEngine.Firefox => _playwright.Firefox,
                BrowserEngine.Webkit => _playwright.Webkit,
                _ => _playwright.Chromium,
            };

            LogStartingBrowser(opts.Engine.ToString());
            _browser = await type.LaunchAsync(launch).ConfigureAwait(false);
            LogBrowserStarted(opts.Engine.ToString());
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task ApplySandboxAsync(IPage page)
    {
        if (_sandbox is null)
        {
            return;
        }
        if (_sandbox.BlockNetworkRequests)
        {
            await page.RouteAsync("**/*", route => route.AbortAsync()).ConfigureAwait(false);
            return;
        }
        if (_sandbox.DisableImages || (_sandbox.BlockedUrlPatterns?.Count ?? 0) > 0)
        {
            await page.RouteAsync("**/*", route =>
            {
                if (_sandbox.DisableImages && route.Request.ResourceType == "image")
                {
                    return route.AbortAsync();
                }
                if (_sandbox.BlockedUrlPatterns is { Count: > 0 } patterns)
                {
                    foreach (string pattern in patterns)
                    {
                        if (route.Request.Url.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            return route.AbortAsync();
                        }
                    }
                }
                return route.ContinueAsync();
            }).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        _drained = true;
        int total = _browsingOptions.Value.MaxBrowsers * _browsingOptions.Value.MaxPagesPerBrowser;
        for (int i = 0; i < total; i++)
        {
            await _pageSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        if (_browser is not null)
        {
            await _browser.CloseAsync().ConfigureAwait(false);
            _browser = null;
        }
        _playwright?.Dispose();
        _playwright = null;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync().ConfigureAwait(false);
            _browser = null;
        }
        _playwright?.Dispose();
        _playwright = null;
        _pageSemaphore.Dispose();
        _initLock.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.Playwright launching {Engine} (headless)...")]
    private partial void LogStartingBrowser(string engine);

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing.Playwright {Engine} launched.")]
    private partial void LogBrowserStarted(string engine);
}
