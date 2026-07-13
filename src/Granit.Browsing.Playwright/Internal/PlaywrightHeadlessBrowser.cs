using System.Diagnostics;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Options;
using Granit.Browsing.Pages;
using Granit.Browsing.Playwright.Options;
using Granit.Browsing.Pool;
using Granit.Browsing.Sandbox;
using Granit.Events;
using Granit.Guids;
using Granit.Http.UrlSafety;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using IClock = Granit.Timing.IClock;

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
    private readonly ILoggerFactory _loggerFactory;
    private readonly IBrowserSandboxProfile _sandbox;
    private readonly IUrlSafetyValidator _urlValidator;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILocalEventBus? _eventBus;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _pageSemaphore;
    private readonly string _engineName;
    private readonly BrowserCapabilities _capabilities;
    private IPlaywright? _playwright;
    private volatile IBrowser? _browser;
    private int _activePages;
    private int _waitingAcquisitions;
    private long _totalAcquisitions;
    private bool _drained;

    public PlaywrightHeadlessBrowser(
        IOptions<GranitBrowsingOptions> browsingOptions,
        IOptions<PlaywrightOptions> playwrightOptions,
        BrowsingMetrics metrics,
        ILogger<PlaywrightHeadlessBrowser> logger,
        ILoggerFactory loggerFactory,
        IBrowserSandboxProfile sandbox,
        IUrlSafetyValidator urlValidator,
        IHostEnvironment hostEnvironment,
        IClock clock,
        IGuidGenerator guidGenerator,
        IServiceScopeFactory? scopeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(browsingOptions);
        ArgumentNullException.ThrowIfNull(playwrightOptions);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(urlValidator);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(guidGenerator);

        _browsingOptions = browsingOptions;
        _playwrightOptions = playwrightOptions;
        _metrics = metrics;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _sandbox = sandbox;
        _urlValidator = urlValidator;
        _hostEnvironment = hostEnvironment;
        _clock = clock;
        _guidGenerator = guidGenerator;
        // Wrap the scope factory so each event publish creates a fresh DI scope —
        // ILocalEventBus is scoped and would fail ValidateScopes if captured directly.
        _eventBus = ScopedLocalEventBus.TryCreate(scopeFactory);

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
                _metrics.RecordError(EngineName, tenantId: null, "acquire_timeout");
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

            BrowserNewContextOptions contextOptions = BuildContextOptions(options, _sandbox);
            IBrowserContext context = await _browser!.NewContextAsync(contextOptions).ConfigureAwait(false);

            if (options?.Cookies is { Count: > 0 } cookies)
            {
                await context.AddCookiesAsync(MapCookies(cookies)).ConfigureAwait(false);
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

            Guid pageId = _guidGenerator.Create();
            TimeSpan? maxRender = _sandbox.MaxRenderDuration
                ?? _browsingOptions.Value.ResourceLimits.MaxRenderDuration;

            var router = new RequestRouter(
                _sandbox,
                _urlValidator,
                _metrics,
                _loggerFactory.CreateLogger<RequestRouter>(),
                _clock,
                EngineName,
                pageId,
                _eventBus);

            var playwrightRouter = new PlaywrightRequestRouter(
                playwrightPage,
                router,
                _loggerFactory.CreateLogger<PlaywrightRequestRouter>());

            if (RequiresEagerInterception(_sandbox))
            {
                await playwrightRouter.EnsureSubscribedAsync().ConfigureAwait(false);
            }

            Interlocked.Increment(ref _activePages);
            Interlocked.Increment(ref _totalAcquisitions);
            const string? tenantId = null;
            _metrics.RecordPageAcquired(EngineName, tenantId);
            _metrics.RecordAcquireDuration(EngineName, tenantId, sw.Elapsed);

            return new PlaywrightBrowserPage(
                playwrightPage,
                context,
                EngineName,
                OnPageReleased,
                playwrightRouter,
                _sandbox,
                _urlValidator,
                maxRender,
                _clock,
                _loggerFactory.CreateLogger<PlaywrightBrowserPage>(),
                pageId,
                _eventBus);
        }
        catch
        {
            _pageSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Maps <see cref="BrowserPageOptions"/> + sandbox profile to Playwright's
    /// <see cref="BrowserNewContextOptions"/>. Pure mapping — extracted from the
    /// acquire hot path so its cognitive complexity stays focused on lifecycle.
    /// </summary>
    private static BrowserNewContextOptions BuildContextOptions(BrowserPageOptions? options, IBrowserSandboxProfile sandbox)
    {
        var contextOptions = new BrowserNewContextOptions
        {
            DeviceScaleFactor = (float?)options?.DeviceScaleFactor,
            UserAgent = options?.UserAgent,
            Locale = options?.Locale,
            TimezoneId = options?.TimezoneId,
            // Sandbox profile takes precedence over per-call BrowserPageOptions for JS enablement.
            JavaScriptEnabled = !sandbox.DisableJavaScript && (options?.JavaScriptEnabled ?? true),
            ColorScheme = options?.ColorScheme switch
            {
                "light" => ColorScheme.Light,
                "dark" => ColorScheme.Dark,
                "no-preference" => ColorScheme.NoPreference,
                _ => null,
            },
        };

        if (options?.Viewport is { } vp)
        {
            contextOptions.ViewportSize = new Microsoft.Playwright.ViewportSize { Width = vp.Width, Height = vp.Height };
        }

        if (options?.ExtraHeaders is { Count: > 0 } headers)
        {
            contextOptions.ExtraHTTPHeaders = new Dictionary<string, string>(headers);
        }

        return contextOptions;
    }

    /// <summary>
    /// Maps the Granit <see cref="CookieParam"/> list to Playwright's <see cref="Cookie"/>
    /// list. Pure mapping — extracted from the acquire hot path.
    /// </summary>
    private static List<Cookie> MapCookies(IReadOnlyList<CookieParam> cookies)
    {
        List<Cookie> mapped = new(cookies.Count);
        foreach (CookieParam c in cookies)
        {
            Cookie cookie = new()
            {
                Name = c.Name,
                Value = c.Value,
                Domain = c.Domain,
                Path = c.Path,
                Url = c.Url,
                HttpOnly = c.HttpOnly,
                Secure = c.Secure,
                SameSite = c.SameSite switch
                {
                    "Strict" => SameSiteAttribute.Strict,
                    "Lax" => SameSiteAttribute.Lax,
                    "None" => SameSiteAttribute.None,
                    _ => null,
                },
            };
            if (c.Expires is { } exp)
            {
                cookie.Expires = exp;
            }
            mapped.Add(cookie);
        }
        return mapped;
    }

    private static bool RequiresEagerInterception(IBrowserSandboxProfile sandbox) =>
        sandbox.BlockNetworkRequests
        || sandbox.DisableImages
        || (sandbox.BlockedUrlPatterns?.Count ?? 0) > 0
        || (sandbox.AllowedHostPatterns?.Count ?? 0) > 0
        || (sandbox.DeniedHostPatterns?.Count ?? 0) > 0
        || sandbox.BlockPrivateNetworks
        || sandbox.AllowedSchemes is { Count: > 0 };

    private void OnPageReleased()
    {
        Interlocked.Decrement(ref _activePages);
        _pageSemaphore.Release();
        _metrics.RecordPageReleased(EngineName, tenantId: null);
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
                try
                {
                    await _browser.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogDisposeFailure(ex);
                }
                _browser = null;
            }

            PlaywrightOptions opts = _playwrightOptions.Value;

            // Refuse privileged flags outside a vetted container. Playwright
            // doesn't expose a DisableSandbox option, but a caller could smuggle
            // --no-sandbox through ExtraArgs — PrivilegedFlagGuard scans for it.
            PrivilegedFlagGuard.EnsureSafe(disableSandbox: false, opts.ExtraArgs, _logger);

            // Refuse to start when production hosts have not pre-provisioned browsers.
            PlaywrightInstallGuard.EnsureBrowsersProvisioned(opts, _hostEnvironment);

            // Refuse a browser binary outside the sandbox-allowed prefix, and refuse a
            // production deploy that overrides the executable without an allowlist prefix.
            string? executablePath = BrowserExecutablePathValidator.Validate(
                opts.ExecutablePath,
                _sandbox.AllowedExecutablePathPrefix,
                nameof(opts.ExecutablePath),
                _hostEnvironment);

            _playwright ??= await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);

            BrowserTypeLaunchOptions launch = new()
            {
                Headless = true,
                ExecutablePath = executablePath,
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
            try
            {
                await _browser.CloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDisposeFailure(ex);
            }
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
            try
            {
                await _browser.CloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDisposeFailure(ex);
            }
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.Playwright failed to dispose a stale browser handle.")]
    private partial void LogDisposeFailure(Exception exception);
}
