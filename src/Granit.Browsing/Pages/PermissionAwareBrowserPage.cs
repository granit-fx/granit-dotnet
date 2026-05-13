using System;
using System.Threading;
using System.Threading.Tasks;
using Granit.Authorization;
using Granit.Browsing.Options;
using Granit.Browsing.Permissions;

namespace Granit.Browsing.Pages;

/// <summary>
/// <see cref="IBrowserPage"/> decorator that enforces <see cref="BrowsingPermissions"/>
/// before delegating to the inner page. Wrapped onto a page by
/// <c>TenantAwareHeadlessBrowser</c> when an <see cref="IPermissionChecker"/> is
/// registered. Throws <see cref="UnauthorizedAccessException"/> on a missing grant.
/// </summary>
internal sealed class PermissionAwareBrowserPage(IBrowserPage inner, IPermissionChecker permissionChecker) : IBrowserPage
{
    /// <inheritdoc/>
    public string EngineName => inner.EngineName;

    /// <inheritdoc/>
    public IObservable<ConsoleMessage> ConsoleMessages => inner.ConsoleMessages;

    /// <inheritdoc/>
    public IObservable<PageError> PageErrors => inner.PageErrors;

    /// <inheritdoc/>
    public async Task NavigateAsync(Uri url, NavigationOptions? options = null, CancellationToken cancellationToken = default)
    {
        await EnsureGrantedAsync(BrowsingPermissions.Pages.Navigate, cancellationToken).ConfigureAwait(false);
        await inner.NavigateAsync(url, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetContentAsync(string html, NavigationOptions? options = null, CancellationToken cancellationToken = default)
    {
        await EnsureGrantedAsync(BrowsingPermissions.Pages.Navigate, cancellationToken).ConfigureAwait(false);
        await inner.SetContentAsync(html, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<string> GetCurrentUrlAsync(CancellationToken cancellationToken = default) =>
        inner.GetCurrentUrlAsync(cancellationToken);

    /// <inheritdoc/>
    public Task WaitForLoadStateAsync(LoadState state, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
        inner.WaitForLoadStateAsync(state, timeout, cancellationToken);

    /// <inheritdoc/>
    public Task WaitForSelectorAsync(string selector, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
        inner.WaitForSelectorAsync(selector, timeout, cancellationToken);

    /// <inheritdoc/>
    public async Task WaitForFunctionAsync(string jsExpression, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        await EnsureGrantedAsync(BrowsingPermissions.Pages.InjectScript, cancellationToken).ConfigureAwait(false);
        await inner.WaitForFunctionAsync(jsExpression, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<TResult?> EvaluateAsync<TResult>(string jsExpression, CancellationToken cancellationToken = default)
    {
        await EnsureGrantedAsync(BrowsingPermissions.Pages.InjectScript, cancellationToken).ConfigureAwait(false);
        return await inner.EvaluateAsync<TResult>(jsExpression, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddStyleTagAsync(string css, CancellationToken cancellationToken = default)
    {
        await EnsureGrantedAsync(BrowsingPermissions.Pages.InjectScript, cancellationToken).ConfigureAwait(false);
        await inner.AddStyleTagAsync(css, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddScriptTagAsync(string js, CancellationToken cancellationToken = default)
    {
        await EnsureGrantedAsync(BrowsingPermissions.Pages.InjectScript, cancellationToken).ConfigureAwait(false);
        await inner.AddScriptTagAsync(js, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<byte[]> ScreenshotAsync(ScreenshotOptions options, CancellationToken cancellationToken = default) =>
        inner.ScreenshotAsync(options, cancellationToken);

    /// <inheritdoc/>
    public Task RouteAsync(
        RoutePattern pattern,
        Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>> handler,
        CancellationToken cancellationToken = default) =>
        inner.RouteAsync(pattern, handler, cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private async Task EnsureGrantedAsync(string permission, CancellationToken cancellationToken)
    {
        bool granted = await permissionChecker.IsGrantedAsync(permission, cancellationToken).ConfigureAwait(false);
        if (!granted)
        {
            throw new UnauthorizedAccessException(
                $"Permission '{permission}' is required to perform this browser-page operation.");
        }
    }
}
