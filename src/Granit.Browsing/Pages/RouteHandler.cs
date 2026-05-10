using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Granit.Browsing.Pages;

/// <summary>
/// Asynchronous handler for an intercepted request — internal provider-side
/// collaborator. End-user code subscribes through
/// <see cref="IBrowserPage.RouteAsync(RoutePattern, System.Func{RouteRequest, CancellationToken, ValueTask{RouteDecision}}, CancellationToken)"/>;
/// providers translate the user handler into one of these.
/// </summary>
/// <param name="context">Operation surface tied to the intercepted request.</param>
/// <param name="cancellationToken">Cancellation token.</param>
internal delegate Task RouteHandler(IRouteContext context, CancellationToken cancellationToken);

/// <summary>Operation surface for a single intercepted request handled by a <see cref="RouteHandler"/>.</summary>
internal interface IRouteContext
{
    /// <summary>The request URL.</summary>
    string Url { get; }

    /// <summary>The HTTP method (GET / POST / …).</summary>
    string Method { get; }

    /// <summary>Request headers (case-insensitive lookup is the provider's responsibility).</summary>
    IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Lets the request continue to the network unmodified.</summary>
    Task ContinueAsync(CancellationToken cancellationToken = default);

    /// <summary>Aborts the request with the supplied error string (provider-defined codes).</summary>
    Task AbortAsync(string errorCode = "failed", CancellationToken cancellationToken = default);

    /// <summary>Fulfills the request with a synthetic response.</summary>
    Task FulfillAsync(int statusCode, IReadOnlyDictionary<string, string>? headers, byte[]? body,
        CancellationToken cancellationToken = default);
}
