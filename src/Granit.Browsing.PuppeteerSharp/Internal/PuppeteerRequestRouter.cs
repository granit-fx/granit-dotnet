using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Pages;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using IPuppeteerPage = PuppeteerSharp.IPage;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// Adapter binding a PuppeteerSharp <c>page.Request</c> event to the provider-neutral
/// <see cref="RequestRouter"/>. Single subscriber per page — funnels every intercepted
/// request through the router so sandbox rules always win and user handlers compose
/// without racing.
/// </summary>
/// <remarks>
/// Avoids the race that occurs when several <c>page.Request +=</c> subscribers compete
/// to call <c>ContinueAsync</c>/<c>AbortAsync</c>, by guaranteeing a single subscription
/// that hand-rolls dispatch under the router's policy chain.
/// </remarks>
internal sealed partial class PuppeteerRequestRouter : IAsyncDisposable
{
    private readonly IPuppeteerPage _page;
    private readonly RequestRouter _router;
    private readonly ILogger<PuppeteerRequestRouter> _logger;
    private readonly ConcurrentDictionary<IRequest, byte> _decided = new();
    private readonly EventHandler<RequestEventArgs> _handler;
    private int _subscribed;
    private int _disposed;

    public PuppeteerRequestRouter(
        IPuppeteerPage page,
        RequestRouter router,
        ILogger<PuppeteerRequestRouter> logger)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(logger);

        _page = page;
        _router = router;
        _logger = logger;

        _handler = (_, e) => _ = HandleRequestAsync(e);
    }

    /// <summary>Exposes the underlying router so the page can register user handlers.</summary>
    internal RequestRouter Router => _router;

    /// <summary>Ensures <c>SetRequestInterceptionAsync(true)</c> is called once.</summary>
    public async Task EnsureSubscribedAsync()
    {
        if (Interlocked.Exchange(ref _subscribed, 1) == 1)
        {
            return;
        }

        await _page.SetRequestInterceptionAsync(true).ConfigureAwait(false);
        _page.Request += _handler;
    }

    private async Task HandleRequestAsync(RequestEventArgs e)
    {
        IRequest req = e.Request;

        // Guard against double-dispatch: if a request was already decided, skip.
        if (!_decided.TryAdd(req, 0))
        {
            return;
        }

        try
        {
            if (!Uri.TryCreate(req.Url, UriKind.Absolute, out Uri? url))
            {
                await SafeAbortAsync(req, RequestAbortErrorCode.Failed).ConfigureAwait(false);
                return;
            }

            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            if (req.Headers is not null)
            {
                foreach (KeyValuePair<string, string> h in req.Headers)
                {
                    headers[h.Key] = h.Value;
                }
            }

            var routeRequest = new RouteRequest(url, req.Method.Method, headers);
            RouteDecision decision = await _router
                .EvaluateAsync(routeRequest, CancellationToken.None)
                .ConfigureAwait(false);

            await ApplyDecisionAsync(req, decision).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogRouteHandlerError(ex, req.Url);
            await SafeAbortAsync(req, RequestAbortErrorCode.Failed).ConfigureAwait(false);
        }
    }

    private static async Task ApplyDecisionAsync(IRequest req, RouteDecision decision)
    {
        switch (decision.Kind)
        {
            case RouteDecisionKind.Abort:
                await req.AbortAsync(MapErrorCode(decision.ErrorCode ?? "failed")).ConfigureAwait(false);
                return;
            case RouteDecisionKind.Fulfill:
                ResponseData payload = new()
                {
                    Status = (HttpStatusCode)decision.StatusCode,
                    BodyData = decision.Body,
                };
                if (decision.Headers is { Count: > 0 } hdrs)
                {
                    Dictionary<string, object> dict = new(hdrs.Count, StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, string> h in hdrs)
                    {
                        dict[h.Key] = h.Value;
                    }
                    payload.Headers = dict;
                }
                await req.RespondAsync(payload).ConfigureAwait(false);
                return;
            default:
                await req.ContinueAsync().ConfigureAwait(false);
                return;
        }
    }

    private async Task SafeAbortAsync(IRequest req, RequestAbortErrorCode code)
    {
        try
        {
            await req.AbortAsync(code).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogAbortFailure(ex, req.Url);
        }
    }

    private static RequestAbortErrorCode MapErrorCode(string code) => code.ToLowerInvariant() switch
    {
        "aborted" => RequestAbortErrorCode.Aborted,
        "accessdenied" => RequestAbortErrorCode.AccessDenied,
        "addressunreachable" => RequestAbortErrorCode.AddressUnreachable,
        "blockedbyclient" => RequestAbortErrorCode.BlockedByClient,
        "blockedbyresponse" => RequestAbortErrorCode.BlockedByResponse,
        "connectionaborted" => RequestAbortErrorCode.ConnectionAborted,
        "connectionclosed" => RequestAbortErrorCode.ConnectionClosed,
        "connectionfailed" => RequestAbortErrorCode.ConnectionFailed,
        "connectionrefused" => RequestAbortErrorCode.ConnectionRefused,
        "connectionreset" => RequestAbortErrorCode.ConnectionReset,
        "internetdisconnected" => RequestAbortErrorCode.InternetDisconnected,
        "namenotresolved" => RequestAbortErrorCode.NameNotResolved,
        "timedout" => RequestAbortErrorCode.TimedOut,
        _ => RequestAbortErrorCode.Failed,
    };

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            _page.Request -= _handler;
        }
        catch (Exception ex)
        {
            LogUnsubscribeFailure(ex);
        }

        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.PuppeteerSharp route handler threw while processing {Url}; aborting request.")]
    private partial void LogRouteHandlerError(Exception exception, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.PuppeteerSharp could not abort intercepted request {Url}.")]
    private partial void LogAbortFailure(Exception exception, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.PuppeteerSharp could not unsubscribe page.Request handler.")]
    private partial void LogUnsubscribeFailure(Exception exception);
}
