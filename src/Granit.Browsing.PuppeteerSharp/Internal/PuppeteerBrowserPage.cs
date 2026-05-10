using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Options;
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
/// pool by invoking the release callback supplied at construction.
/// </summary>
internal sealed class PuppeteerBrowserPage(
    IPuppeteerPage page,
    System.Action onReleased) : IBrowserPage
{
    private readonly SimpleObservable<ConsoleMessage> _consoleMessages = new();
    private readonly SimpleObservable<PageError> _pageErrors = new();
    private bool _wired;
    private bool _disposed;

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
        page.Console += (_, e) => _consoleMessages.Publish(new ConsoleMessage(e.Message.Type.ToString(), e.Message.Text));
        page.PageError += (_, e) => _pageErrors.Publish(new PageError(e.Message, StackTrace: null));
        _wired = true;
    }

    /// <inheritdoc/>
    public async Task NavigateAsync(string url, BrowsingNavigationOptions? options = null, CancellationToken cancellationToken = default)
    {
        PuppeteerNavigationOptions nav = ToPuppeteerNavigation(options);
        nav.Referer = options?.Referer;
        await page.GoToAsync(url, nav).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task SetContentAsync(string html, BrowsingNavigationOptions? options = null, CancellationToken cancellationToken = default)
    {
        await page.SetContentAsync(html, ToPuppeteerNavigation(options)).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public Task<string> GetCurrentUrlAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(page.Url);

    /// <inheritdoc/>
    public async Task WaitForLoadStateAsync(LoadState state, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        WaitUntilNavigation waitUntil = state switch
        {
            LoadState.Load => WaitUntilNavigation.Load,
            LoadState.DomContentLoaded => WaitUntilNavigation.DOMContentLoaded,
            LoadState.NetworkIdle => WaitUntilNavigation.Networkidle0,
            _ => WaitUntilNavigation.Load,
        };
        await page.WaitForNavigationAsync(new PuppeteerNavigationOptions
        {
            WaitUntil = [waitUntil],
            Timeout = (int?)timeout?.TotalMilliseconds ?? 30_000,
        }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task WaitForSelectorAsync(string selector, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        await page.WaitForSelectorAsync(selector, new WaitForSelectorOptions
        {
            Timeout = (int?)timeout?.TotalMilliseconds ?? 30_000,
        }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task WaitForFunctionAsync(string jsExpression, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        await page.WaitForFunctionAsync(jsExpression, new WaitForFunctionOptions
        {
            Timeout = (int?)timeout?.TotalMilliseconds ?? 30_000,
        }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task<TResult?> EvaluateAsync<TResult>(string jsExpression, CancellationToken cancellationToken = default)
    {
        TResult result = await page.EvaluateExpressionAsync<TResult>(jsExpression).ConfigureAwait(false);
        _ = cancellationToken;
        return result;
    }

    /// <inheritdoc/>
    public async Task AddStyleTagAsync(string css, CancellationToken cancellationToken = default)
    {
        await page.AddStyleTagAsync(new AddTagOptions { Content = css }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task AddScriptTagAsync(string js, CancellationToken cancellationToken = default)
    {
        await page.AddScriptTagAsync(new AddTagOptions { Content = js }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task<byte[]> ScreenshotAsync(BrowsingScreenshotOptions options, CancellationToken cancellationToken = default)
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

        return await page.ScreenshotDataAsync(puppeteerOpts).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RouteAsync(string urlPattern, RouteHandler handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        await page.SetRequestInterceptionAsync(true).ConfigureAwait(false);
        page.Request += async (_, e) =>
        {
            if (!e.Request.Url.Contains(urlPattern, StringComparison.OrdinalIgnoreCase))
            {
                await e.Request.ContinueAsync().ConfigureAwait(false);
                return;
            }
            await handler(new PuppeteerRouteContext(e.Request), cancellationToken).ConfigureAwait(false);
        };
    }

    /// <summary>Internal accessor for capability implementations.</summary>
    internal IPuppeteerPage UnderlyingPage => page;

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
            await page.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            // Page may already be closed (e.g. browser disposed). Swallow — release is what matters.
        }
        onReleased();
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
}

/// <summary>Naïve in-process <see cref="IObservable{T}"/> used for console / page error feeds.</summary>
internal sealed class SimpleObservable<T> : IObservable<T>
{
    private readonly object _gate = new();
    private readonly List<IObserver<T>> _observers = [];

    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_gate)
        {
            _observers.Add(observer);
        }
        return new Subscription(this, observer);
    }

    public void Publish(T value)
    {
        IObserver<T>[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _observers];
        }
        foreach (IObserver<T> obs in snapshot)
        {
            obs.OnNext(value);
        }
    }

    private sealed class Subscription(SimpleObservable<T> owner, IObserver<T> observer) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._gate)
            {
                owner._observers.Remove(observer);
            }
        }
    }
}
