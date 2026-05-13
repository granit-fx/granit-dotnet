using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Options;
using Granit.Browsing.Pages;
using Granit.Browsing.Pool;
using Granit.Browsing.PuppeteerSharp.Options;
using Granit.Browsing.Sandbox;
using Granit.Events;
using Granit.Guids;
using Granit.Http.Security;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Hosting;
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
internal sealed partial class PuppeteerHeadlessBrowser : IHeadlessBrowser, IHeadlessBrowserPool, IAsyncDisposable
{
    private const string Engine = "chromium-puppeteer";

    private readonly IOptions<GranitBrowsingOptions> _browsingOptions;
    private readonly IOptions<PuppeteerSharpOptions> _puppeteerOptions;
    private readonly BrowsingMetrics _metrics;
    private readonly ILogger<PuppeteerHeadlessBrowser> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IBrowserSandboxProfile _sandbox;
    private readonly IUrlSafetyValidator _urlValidator;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant? _currentTenant;
    private readonly ILocalEventBus? _eventBus;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _pageSemaphore;
    private volatile IPuppeteerBrowser? _browser;
    private int _activePages;
    private int _waitingAcquisitions;
    private long _totalAcquisitions;
    private bool _drained;

    public PuppeteerHeadlessBrowser(
        IOptions<GranitBrowsingOptions> browsingOptions,
        IOptions<PuppeteerSharpOptions> puppeteerOptions,
        BrowsingMetrics metrics,
        ILogger<PuppeteerHeadlessBrowser> logger,
        ILoggerFactory loggerFactory,
        IBrowserSandboxProfile sandbox,
        IUrlSafetyValidator urlValidator,
        IHostEnvironment hostEnvironment,
        IClock clock,
        IGuidGenerator guidGenerator,
        ICurrentTenant? currentTenant = null,
        ILocalEventBus? eventBus = null)
    {
        ArgumentNullException.ThrowIfNull(browsingOptions);
        ArgumentNullException.ThrowIfNull(puppeteerOptions);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(urlValidator);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(guidGenerator);

        _browsingOptions = browsingOptions;
        _puppeteerOptions = puppeteerOptions;
        _metrics = metrics;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _sandbox = sandbox;
        _urlValidator = urlValidator;
        _hostEnvironment = hostEnvironment;
        _clock = clock;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _eventBus = eventBus;

        int permits = browsingOptions.Value.MaxBrowsers * browsingOptions.Value.MaxPagesPerBrowser;
        _pageSemaphore = new SemaphoreSlim(permits, permits);
    }

    private string? CurrentTenantId =>
        _currentTenant is { IsAvailable: true } t ? t.Id?.ToString("N") : null;

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
            acquireCts.CancelAfter(_browsingOptions.Value.AcquireTimeout);

            try
            {
                await _pageSemaphore.WaitAsync(acquireCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _metrics.RecordError(Engine, CurrentTenantId, "acquire_timeout");
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

            IPuppeteerPage puppeteerPage = await _browser!.NewPageAsync().ConfigureAwait(false);

            // Disable JS before navigation when sandbox/options require it.
            await PuppeteerJsContextGuard.ApplyAsync(
                puppeteerPage,
                pageJsEnabled: options?.JavaScriptEnabled ?? true,
                sandboxDisablesJs: _sandbox.DisableJavaScript).ConfigureAwait(false);

            await ApplyPageOptionsAsync(puppeteerPage, options).ConfigureAwait(false);

            Guid pageId = _guidGenerator.Create();
            TimeSpan? maxRender = _sandbox.MaxRenderDuration
                ?? _browsingOptions.Value.ResourceLimits.MaxRenderDuration;

            var router = new RequestRouter(
                _sandbox,
                _urlValidator,
                _metrics,
                _loggerFactory.CreateLogger<RequestRouter>(),
                _clock,
                Engine,
                pageId,
                _eventBus);

            var puppeteerRouter = new PuppeteerRequestRouter(
                puppeteerPage,
                router,
                _loggerFactory.CreateLogger<PuppeteerRequestRouter>());

            // Pre-subscribe interception when the sandbox requires it. Otherwise the
            // router subscribes lazily on the first RouteAsync / NavigateAsync.
            if (RequiresEagerInterception(_sandbox))
            {
                await puppeteerRouter.EnsureSubscribedAsync().ConfigureAwait(false);
            }

            Interlocked.Increment(ref _activePages);
            Interlocked.Increment(ref _totalAcquisitions);
            string? tenantId = CurrentTenantId;
            _metrics.RecordPageAcquired(Engine, tenantId);
            _metrics.RecordAcquireDuration(Engine, tenantId, stopwatch.Elapsed);

            return new PuppeteerBrowserPage(
                puppeteerPage,
                OnPageReleased,
                puppeteerRouter,
                _sandbox,
                _urlValidator,
                maxRender,
                _clock,
                _loggerFactory.CreateLogger<PuppeteerBrowserPage>(),
                pageId,
                _eventBus);
        }
        catch
        {
            _pageSemaphore.Release();
            throw;
        }
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
        _metrics.RecordPageReleased(Engine, CurrentTenantId);
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
                try
                {
                    await _browser.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogDisposeFailure(ex);
                }
                _browser = null;
            }

            PuppeteerSharpOptions opts = _puppeteerOptions.Value;

            // Refuse privileged flags outside a vetted container.
            PrivilegedFlagGuard.EnsureSafe(opts.DisableSandbox, opts.ExtraArgs, _logger);

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

            // Refuse a Chromium binary outside the sandbox-allowed prefix.
            string? executablePath = PuppeteerExecutablePathValidator.Validate(
                opts.ChromiumExecutablePath,
                _sandbox.AllowedExecutablePathPrefix);

            PuppeteerLaunchOptions launch = new()
            {
                Headless = true,
                Args = [.. args],
            };
            if (!string.IsNullOrEmpty(executablePath))
            {
                launch.ExecutablePath = executablePath;
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
        BrowserPageOptions? options)
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

        if (!string.IsNullOrEmpty(options.MediaType))
        {
            await page.EmulateMediaTypeAsync(options.MediaType.Equals("print", StringComparison.OrdinalIgnoreCase)
                ? global::PuppeteerSharp.Media.MediaType.Print
                : global::PuppeteerSharp.Media.MediaType.Screen).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        _drained = true;
        // Wait for in-flight pages to complete by acquiring all permits.
        int total = _browsingOptions.Value.MaxBrowsers * _browsingOptions.Value.MaxPagesPerBrowser;
        for (int i = 0; i < total; i++)
        {
            await _pageSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_browser is not null)
        {
            try
            {
                await _browser.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDisposeFailure(ex);
            }
            _browser = null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            try
            {
                await _browser.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDisposeFailure(ex);
            }
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.PuppeteerSharp failed to dispose a stale Chromium browser handle.")]
    private partial void LogDisposeFailure(Exception exception);
}
