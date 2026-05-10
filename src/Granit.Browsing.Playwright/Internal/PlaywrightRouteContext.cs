using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>Microsoft.Playwright-backed <see cref="IRouteContext"/>.</summary>
internal sealed class PlaywrightRouteContext(IRoute route) : IRouteContext
{
    /// <inheritdoc/>
    public string Url => route.Request.Url;

    /// <inheritdoc/>
    public string Method => route.Request.Method;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Headers
    {
        get
        {
            Dictionary<string, string> dict = new(System.StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> h in route.Request.Headers)
            {
                dict[h.Key] = h.Value;
            }
            return dict;
        }
    }

    /// <inheritdoc/>
    public Task ContinueAsync(CancellationToken cancellationToken = default) => route.ContinueAsync();

    /// <inheritdoc/>
    public Task AbortAsync(string errorCode = "failed", CancellationToken cancellationToken = default) =>
        route.AbortAsync(errorCode);

    /// <inheritdoc/>
    public Task FulfillAsync(int statusCode, IReadOnlyDictionary<string, string>? headers, byte[]? body,
        CancellationToken cancellationToken = default)
    {
        var opts = new RouteFulfillOptions
        {
            Status = statusCode,
            BodyBytes = body,
        };
        if (headers is not null)
        {
            opts.Headers = new Dictionary<string, string>(headers);
        }
        return route.FulfillAsync(opts);
    }
}
