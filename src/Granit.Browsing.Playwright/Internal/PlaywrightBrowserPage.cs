using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using BrowsingNavigationOptions = Granit.Browsing.Options.NavigationOptions;
using BrowsingScreenshotFormat = Granit.Browsing.Options.ScreenshotFormat;
using BrowsingScreenshotOptions = Granit.Browsing.Options.ScreenshotOptions;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>Microsoft.Playwright-backed <see cref="IBrowserPage"/>.</summary>
internal sealed class PlaywrightBrowserPage(
    IPage page,
    IBrowserContext context,
    string engineName,
    System.Action onReleased) : IBrowserPage
{
    private readonly SimpleObservable<ConsoleMessage> _consoleMessages = new();
    private readonly SimpleObservable<PageError> _pageErrors = new();
    private bool _wired;
    private bool _disposed;

    /// <inheritdoc/>
    public string EngineName => engineName;

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
        page.Console += (_, msg) => _consoleMessages.Publish(new ConsoleMessage(msg.Type, msg.Text));
        page.PageError += (_, err) => _pageErrors.Publish(new PageError(err, StackTrace: null));
        _wired = true;
    }

    /// <inheritdoc/>
    public async Task NavigateAsync(string url, BrowsingNavigationOptions? options = null, CancellationToken cancellationToken = default)
    {
        await page.GotoAsync(url, new PageGotoOptions
        {
            Timeout = (float?)options?.Timeout?.TotalMilliseconds,
            WaitUntil = MapWaitUntil(options?.WaitUntil ?? LoadState.Load),
            Referer = options?.Referer,
        }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task SetContentAsync(string html, BrowsingNavigationOptions? options = null, CancellationToken cancellationToken = default)
    {
        await page.SetContentAsync(html, new PageSetContentOptions
        {
            Timeout = (float?)options?.Timeout?.TotalMilliseconds,
            WaitUntil = MapWaitUntil(options?.WaitUntil ?? LoadState.Load),
        }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public Task<string> GetCurrentUrlAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(page.Url);

    /// <inheritdoc/>
    public async Task WaitForLoadStateAsync(LoadState state, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        await page.WaitForLoadStateAsync(state switch
        {
            LoadState.DomContentLoaded => Microsoft.Playwright.LoadState.DOMContentLoaded,
            LoadState.NetworkIdle => Microsoft.Playwright.LoadState.NetworkIdle,
            _ => Microsoft.Playwright.LoadState.Load,
        }, new PageWaitForLoadStateOptions
        {
            Timeout = (float?)timeout?.TotalMilliseconds,
        }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task WaitForSelectorAsync(string selector, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
        {
            Timeout = (float?)timeout?.TotalMilliseconds,
        }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task WaitForFunctionAsync(string jsExpression, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        await page.WaitForFunctionAsync(jsExpression, arg: null, new PageWaitForFunctionOptions
        {
            Timeout = (float?)timeout?.TotalMilliseconds,
        }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task<TResult?> EvaluateAsync<TResult>(string jsExpression, CancellationToken cancellationToken = default)
    {
        TResult? result = await page.EvaluateAsync<TResult>(jsExpression).ConfigureAwait(false);
        _ = cancellationToken;
        return result;
    }

    /// <inheritdoc/>
    public async Task AddStyleTagAsync(string css, CancellationToken cancellationToken = default)
    {
        await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = css }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task AddScriptTagAsync(string js, CancellationToken cancellationToken = default)
    {
        await page.AddScriptTagAsync(new PageAddScriptTagOptions { Content = js }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task<byte[]> ScreenshotAsync(BrowsingScreenshotOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ScreenshotType type = options.Format switch
        {
            BrowsingScreenshotFormat.Jpeg => ScreenshotType.Jpeg,
            _ => ScreenshotType.Png,
        };
        // Playwright's screenshot type enum doesn't have WebP; fall back to PNG.

        var pwOpts = new PageScreenshotOptions
        {
            Type = type,
            FullPage = options.FullPage,
            OmitBackground = options.OmitBackground,
        };
        if (options.Quality is { } q)
        {
            pwOpts.Quality = q;
        }
        if (options.Clip is { } c)
        {
            pwOpts.Clip = new Clip { X = (float)c.X, Y = (float)c.Y, Width = (float)c.Width, Height = (float)c.Height };
        }
        byte[] result = await page.ScreenshotAsync(pwOpts).ConfigureAwait(false);
        _ = cancellationToken;
        return result;
    }

    /// <inheritdoc/>
    public async Task RouteAsync(string urlPattern, RouteHandler handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        await page.RouteAsync(urlPattern, async route =>
        {
            await handler(new PlaywrightRouteContext(route), cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>Internal accessor for capability impls.</summary>
    internal IPage UnderlyingPage => page;

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
            await context.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            // Page or context may already be closed; release is what matters.
        }
        onReleased();
    }

    private static WaitUntilState MapWaitUntil(LoadState state) => state switch
    {
        LoadState.DomContentLoaded => WaitUntilState.DOMContentLoaded,
        LoadState.NetworkIdle => WaitUntilState.NetworkIdle,
        _ => WaitUntilState.Load,
    };
}

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
