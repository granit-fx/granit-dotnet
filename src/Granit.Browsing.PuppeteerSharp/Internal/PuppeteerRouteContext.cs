using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>PuppeteerSharp-backed <see cref="IRouteContext"/>.</summary>
internal sealed class PuppeteerRouteContext(IRequest request) : IRouteContext
{
    /// <inheritdoc/>
    public string Url => request.Url;

    /// <inheritdoc/>
    public string Method => request.Method.Method;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Headers
    {
        get
        {
            Dictionary<string, string> dict = new(System.StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> h in request.Headers)
            {
                dict[h.Key] = h.Value;
            }
            return dict;
        }
    }

    /// <inheritdoc/>
    public Task ContinueAsync(CancellationToken cancellationToken = default) =>
        request.ContinueAsync();

    /// <inheritdoc/>
    public Task AbortAsync(string errorCode = "failed", CancellationToken cancellationToken = default) =>
        request.AbortAsync(MapErrorCode(errorCode));

    /// <inheritdoc/>
    public Task FulfillAsync(int statusCode, IReadOnlyDictionary<string, string>? headers, byte[]? body,
        CancellationToken cancellationToken = default)
    {
        ResponseData payload = new()
        {
            Status = (System.Net.HttpStatusCode)statusCode,
            BodyData = body,
        };
        if (headers is not null)
        {
            Dictionary<string, object> headerDict = new(headers.Count, System.StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> h in headers)
            {
                headerDict[h.Key] = h.Value;
            }
            payload.Headers = headerDict;
        }
        return request.RespondAsync(payload);
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
}
