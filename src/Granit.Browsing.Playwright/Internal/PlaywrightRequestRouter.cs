using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>
/// Adapter binding a Playwright <c>page.RouteAsync("**\/*", …)</c> subscriber to the
/// provider-neutral <see cref="RequestRouter"/>. Single subscription per page —
/// funnels every intercepted request through the router so sandbox rules always win
/// and user handlers compose without racing.
/// </summary>
/// <remarks>
/// Closes VULN-101 (provider-specific race when several inline <c>page.RouteAsync</c>
/// handlers compete to call <c>ContinueAsync</c>/<c>AbortAsync</c>).
/// </remarks>
internal sealed partial class PlaywrightRequestRouter : IAsyncDisposable
{
    private const string CatchAllPattern = "**/*";

    private readonly IPage _page;
    private readonly RequestRouter _router;
    private readonly ILogger<PlaywrightRequestRouter> _logger;
    private readonly Func<IRoute, Task> _handler;
    private int _subscribed;
    private int _disposed;

    public PlaywrightRequestRouter(
        IPage page,
        RequestRouter router,
        ILogger<PlaywrightRequestRouter> logger)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(logger);

        _page = page;
        _router = router;
        _logger = logger;
        _handler = HandleRouteAsync;
    }

    /// <summary>Exposes the underlying router so the page can register user handlers.</summary>
    internal RequestRouter Router => _router;

    /// <summary>Ensures the catch-all <c>page.RouteAsync</c> handler is subscribed exactly once.</summary>
    public async Task EnsureSubscribedAsync()
    {
        if (Interlocked.Exchange(ref _subscribed, 1) == 1)
        {
            return;
        }

        await _page.RouteAsync(CatchAllPattern, _handler).ConfigureAwait(false);
    }

    private async Task HandleRouteAsync(IRoute route)
    {
        try
        {
            if (!Uri.TryCreate(route.Request.Url, UriKind.Absolute, out Uri? url))
            {
                await SafeAbortAsync(route, "failed").ConfigureAwait(false);
                return;
            }

            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> h in route.Request.Headers)
            {
                headers[h.Key] = h.Value;
            }

            var routeRequest = new RouteRequest(url, route.Request.Method, headers);
            RouteDecision decision = await _router
                .EvaluateAsync(routeRequest, CancellationToken.None)
                .ConfigureAwait(false);

            await ApplyDecisionAsync(route, decision).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogRouteHandlerError(ex, route.Request.Url);
            await SafeAbortAsync(route, "failed").ConfigureAwait(false);
        }
    }

    private static async Task ApplyDecisionAsync(IRoute route, RouteDecision decision)
    {
        switch (decision.Kind)
        {
            case RouteDecisionKind.Abort:
                await route.AbortAsync(decision.ErrorCode ?? "failed").ConfigureAwait(false);
                return;
            case RouteDecisionKind.Fulfill:
                var fulfill = new RouteFulfillOptions
                {
                    Status = decision.StatusCode,
                    BodyBytes = decision.Body,
                };
                if (decision.Headers is { Count: > 0 } hdrs)
                {
                    Dictionary<string, string> dict = new(hdrs.Count, StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, string> h in hdrs)
                    {
                        dict[h.Key] = h.Value;
                    }
                    fulfill.Headers = dict;
                }
                await route.FulfillAsync(fulfill).ConfigureAwait(false);
                return;
            default:
                await route.ContinueAsync().ConfigureAwait(false);
                return;
        }
    }

    private async Task SafeAbortAsync(IRoute route, string errorCode)
    {
        try
        {
            await route.AbortAsync(errorCode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogAbortFailure(ex, route.Request.Url);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (Volatile.Read(ref _subscribed) == 1)
        {
            try
            {
                await _page.UnrouteAsync(CatchAllPattern, _handler).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUnsubscribeFailure(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.Playwright route handler threw while processing {Url}; aborting request.")]
    private partial void LogRouteHandlerError(Exception exception, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.Playwright could not abort intercepted request {Url}.")]
    private partial void LogAbortFailure(Exception exception, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.Playwright could not unsubscribe page route handler.")]
    private partial void LogUnsubscribeFailure(Exception exception);
}
