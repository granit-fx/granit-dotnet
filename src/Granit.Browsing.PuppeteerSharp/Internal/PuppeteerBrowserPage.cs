using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Internal;
using Granit.Browsing.Options;
using Granit.Browsing.Pages;
using Granit.Browsing.Pool;
using Granit.Browsing.Sandbox;
using Granit.Events;
using Granit.Http.Security;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using BrowsingNavigationOptions = Granit.Browsing.Options.NavigationOptions;
using BrowsingScreenshotFormat = Granit.Browsing.Options.ScreenshotFormat;
using BrowsingScreenshotOptions = Granit.Browsing.Options.ScreenshotOptions;
using IPuppeteerPage = PuppeteerSharp.IPage;
using PuppeteerNavigationOptions = PuppeteerSharp.NavigationOptions;
using PuppeteerScreenshotOptions = PuppeteerSharp.ScreenshotOptions;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// PuppeteerSharp-backed <see cref="IBrowserPage"/>. Disposing returns the page to the
/// pool by invoking the release callback supplied at construction. The page owns a
/// <see cref="PuppeteerRequestRouter"/> that funnels every intercepted request through
/// the sandbox + user-handler chain.
/// </summary>
internal sealed partial class PuppeteerBrowserPage : IBrowserPage
{
    private readonly IPuppeteerPage _page;
    private readonly Action _onReleased;
    private readonly PuppeteerRequestRouter _router;
    private readonly IBrowserSandboxProfile _sandbox;
    private readonly IUrlSafetyValidator _urlValidator;
    private readonly TimeSpan? _maxRenderDuration;
    private readonly ILocalEventBus? _eventBus;
    private readonly IClock _clock;
    private readonly ILogger<PuppeteerBrowserPage> _logger;
    private readonly Guid _pageId;
    private readonly SimpleObservable<ConsoleMessage> _consoleMessages;
    private readonly SimpleObservable<PageError> _pageErrors;
    private bool _wired;
    private bool _disposed;

    public PuppeteerBrowserPage(
        IPuppeteerPage page,
        Action onReleased,
        PuppeteerRequestRouter router,
        IBrowserSandboxProfile sandbox,
        IUrlSafetyValidator urlValidator,
        TimeSpan? maxRenderDuration,
        IClock clock,
        ILogger<PuppeteerBrowserPage> logger,
        Guid pageId,
        ILocalEventBus? eventBus = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(onReleased);
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(urlValidator);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _page = page;
        _onReleased = onReleased;
        _router = router;
        _sandbox = sandbox;
        _urlValidator = urlValidator;
        _maxRenderDuration = maxRenderDuration;
        _clock = clock;
        _logger = logger;
        _pageId = pageId;
        _eventBus = eventBus;

        _consoleMessages = new SimpleObservable<ConsoleMessage>(logger);
        _pageErrors = new SimpleObservable<PageError>(logger);
    }

    /// <inheritdoc/>
    public string EngineName => "chromium-puppeteer";

    /// <inheritdoc/>
    public IObservable<ConsoleMessage> ConsoleMessages
    {
        get
        {
            EnsureWired();
            return _consoleMessages;
        }
    }

    /// <inheritdoc/>
    public IObservable<PageError> PageErrors
    {
        get
        {
            EnsureWired();
            return _pageErrors;
        }
    }

    private void EnsureWired()
    {
        if (_wired)
        {
            return;
        }
        _page.Console += (_, e) =>
        {
            string text = e.Message.Text;
            if (_sandbox.RedactConsoleMessages)
            {
                text = ConsoleRedactor.Redact(text);
            }
            _consoleMessages.Publish(new ConsoleMessage(e.Message.Type.ToString(), text));
        };
        _page.PageError += (_, e) =>
        {
            string message = e.Message;
            if (_sandbox.RedactConsoleMessages)
            {
                message = ConsoleRedactor.Redact(message);
            }
            _pageErrors.Publish(new PageError(message, StackTrace: null));
        };
        _wired = true;
    }

    /// <inheritdoc/>
    public async Task NavigateAsync(Uri url, BrowsingNavigationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        UrlSafetyResult safety = await _urlValidator
            .ValidateAsync(url, cancellationToken)
            .ConfigureAwait(false);

        if (!safety.IsValid)
        {
            string reason = safety.Violation?.Reason ?? "url_safety_violation";
            await PublishNavigatedAsync(url, blocked: true, reason, cancellationToken).ConfigureAwait(false);
            throw new SandboxViolationException(SandboxViolationKind.UrlSafetyViolation, reason);
        }

        await _router.EnsureSubscribedAsync().ConfigureAwait(false);

        PuppeteerNavigationOptions nav = ToPuppeteerNavigation(options);
        nav.Referer = options?.Referer;

        await BrowsingTimeout.RunAsync(
            ct => _page.GoToAsync(url.ToString(), nav),
            _maxRenderDuration,
            cancellationToken).ConfigureAwait(false);

        await PublishNavigatedAsync(url, blocked: false, reason: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task SetContentAsync(string html, BrowsingNavigationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        return BrowsingTimeout.RunAsync(
            ct => _page.SetContentAsync(html, ToPuppeteerNavigation(options)),
            _maxRenderDuration,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> GetCurrentUrlAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_page.Url);

    /// <inheritdoc/>
    public Task WaitForLoadStateAsync(LoadState state, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        WaitUntilNavigation waitUntil = state switch
        {
            LoadState.Load => WaitUntilNavigation.Load,
            LoadState.DomContentLoaded => WaitUntilNavigation.DOMContentLoaded,
            LoadState.NetworkIdle => WaitUntilNavigation.Networkidle0,
            _ => WaitUntilNavigation.Load,
        };
        return BrowsingTimeout.RunAsync(
            ct => _page.WaitForNavigationAsync(new PuppeteerNavigationOptions
            {
                WaitUntil = [waitUntil],
                Timeout = (int?)timeout?.TotalMilliseconds ?? 30_000,
            }),
            _maxRenderDuration,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task WaitForSelectorAsync(string selector, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        return BrowsingTimeout.RunAsync(
            async ct =>
            {
                await _page.WaitForSelectorAsync(selector, new WaitForSelectorOptions
                {
                    Timeout = (int?)timeout?.TotalMilliseconds ?? 30_000,
                }).ConfigureAwait(false);
            },
            _maxRenderDuration,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task WaitForFunctionAsync(string jsExpression, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(jsExpression);
        PublishScriptInjected("evaluate", jsExpression);
        return BrowsingTimeout.RunAsync(
            async ct =>
            {
                await _page.WaitForFunctionAsync(jsExpression, new WaitForFunctionOptions
                {
                    Timeout = (int?)timeout?.TotalMilliseconds ?? 30_000,
                }).ConfigureAwait(false);
            },
            _maxRenderDuration,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TResult?> EvaluateAsync<TResult>(string jsExpression, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(jsExpression);
        PublishScriptInjected("evaluate", jsExpression);
        return BrowsingTimeout.RunAsync<TResult?>(
            async ct => await _page.EvaluateExpressionAsync<TResult>(jsExpression).ConfigureAwait(false),
            _maxRenderDuration,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task AddStyleTagAsync(string css, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(css);
        PublishScriptInjected("style", css);
        return BrowsingTimeout.RunAsync(
            async ct =>
            {
                await _page.AddStyleTagAsync(new AddTagOptions { Content = css }).ConfigureAwait(false);
            },
            _maxRenderDuration,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task AddScriptTagAsync(string js, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(js);
        PublishScriptInjected("script", js);
        return BrowsingTimeout.RunAsync(
            async ct =>
            {
                await _page.AddScriptTagAsync(new AddTagOptions { Content = js }).ConfigureAwait(false);
            },
            _maxRenderDuration,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<byte[]> ScreenshotAsync(BrowsingScreenshotOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        ScreenshotType type = options.Format switch
        {
            BrowsingScreenshotFormat.Png => ScreenshotType.Png,
            BrowsingScreenshotFormat.Jpeg => ScreenshotType.Jpeg,
            BrowsingScreenshotFormat.Webp => ScreenshotType.Webp,
            _ => ScreenshotType.Png,
        };

        var puppeteerOpts = new PuppeteerScreenshotOptions
        {
            Type = type,
            FullPage = options.FullPage,
            OmitBackground = options.OmitBackground,
        };
        if (options.Quality is { } q)
        {
            puppeteerOpts.Quality = q;
        }
        if (options.Clip is { } c)
        {
            puppeteerOpts.Clip = new Clip
            {
                X = (decimal)c.X,
                Y = (decimal)c.Y,
                Width = (decimal)c.Width,
                Height = (decimal)c.Height,
            };
        }

        return BrowsingTimeout.RunAsync(
            ct => _page.ScreenshotDataAsync(puppeteerOpts),
            _maxRenderDuration,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RouteAsync(
        RoutePattern pattern,
        Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(handler);

        _router.Router.Register(pattern, handler);
        await _router.EnsureSubscribedAsync().ConfigureAwait(false);
    }

    /// <summary>Internal accessor for capability implementations.</summary>
    internal IPuppeteerPage UnderlyingPage => _page;

    /// <summary>Internal accessor — sandbox profile applied to this page.</summary>
    internal IBrowserSandboxProfile Sandbox => _sandbox;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        try
        {
            await _router.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogRouterDisposeFailure(ex);
        }

        try
        {
            await _page.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogPageCloseFailure(ex);
        }

        _onReleased();
    }

    private void PublishScriptInjected(string kind, string script)
    {
        if (_eventBus is null)
        {
            return;
        }

        string hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(script))).ToLowerInvariant();

        try
        {
            _ = _eventBus.PublishAsync(
                new BrowserScriptInjectedEvent(
                    PageId: _pageId,
                    EngineName: EngineName,
                    Kind: kind,
                    ScriptHash: hash,
                    InjectedAt: _clock.Now),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogScriptInjectedPublishFailure(ex);
        }
    }

    private async Task PublishNavigatedAsync(Uri url, bool blocked, string? reason, CancellationToken cancellationToken)
    {
        if (_eventBus is null)
        {
            return;
        }

        try
        {
            await _eventBus.PublishAsync(
                new BrowserUrlNavigatedEvent(
                    PageId: _pageId,
                    EngineName: EngineName,
                    Url: url.ToString(),
                    BlockedBySandbox: blocked,
                    BlockReason: reason,
                    NavigatedAt: _clock.Now),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogNavigatedPublishFailure(ex);
        }
    }

    private static PuppeteerNavigationOptions ToPuppeteerNavigation(BrowsingNavigationOptions? options)
    {
        WaitUntilNavigation waitUntil = options?.WaitUntil switch
        {
            LoadState.DomContentLoaded => WaitUntilNavigation.DOMContentLoaded,
            LoadState.NetworkIdle => WaitUntilNavigation.Networkidle0,
            _ => WaitUntilNavigation.Load,
        };
        return new PuppeteerNavigationOptions
        {
            WaitUntil = [waitUntil],
            Timeout = (int?)options?.Timeout?.TotalMilliseconds ?? 30_000,
        };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.PuppeteerSharp failed to dispose request router.")]
    private partial void LogRouterDisposeFailure(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.PuppeteerSharp failed to close page on dispose.")]
    private partial void LogPageCloseFailure(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.PuppeteerSharp failed to publish BrowserScriptInjectedEvent.")]
    private partial void LogScriptInjectedPublishFailure(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.PuppeteerSharp failed to publish BrowserUrlNavigatedEvent.")]
    private partial void LogNavigatedPublishFailure(Exception exception);
}
