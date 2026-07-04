using System.Diagnostics;

namespace Granit.Geocoding.Internal;

/// <summary>
/// Strips the address from the outbound HTTP trace emitted by the built-in <c>System.Net.Http</c> instrumentation.
/// </summary>
/// <remarks>
/// Geocoding providers pass the address in the request query string (e.g. <c>?street=…&amp;city=…</c> or <c>?q=…</c>),
/// which the instrumentation records verbatim in the span's <c>url.full</c> / <c>url.query</c> tags — leaking personal
/// data (GDPR) into trace exporters. This handler sits inside the diagnostics handler in the <c>HttpClient</c> pipeline,
/// so <see cref="Activity.Current"/> is the live HTTP-client activity; it overwrites those tags before the span is
/// exported. The source-name guard ensures it only ever touches the HTTP-client activity, never an ambient inbound
/// (e.g. ASP.NET Core) activity when tracing is otherwise disabled. Shared by every <c>Granit.Geocoding.*</c> provider
/// registered over a plain <c>HttpClient</c>.
/// </remarks>
internal sealed class AddressTelemetryRedactionHandler : DelegatingHandler
{
    private const string HttpActivitySourceName = "System.Net.Http";
    private const string RedactedQuery = "redacted";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Redact(request.RequestUri);
        return base.SendAsync(request, cancellationToken);
    }

    private static void Redact(Uri? requestUri)
    {
        if (requestUri is null
            || Activity.Current is not { } activity
            || activity.Source.Name != HttpActivitySourceName)
        {
            return;
        }

        // By the time the message reaches the handler pipeline, HttpClient has resolved RequestUri to the absolute
        // BaseAddress-combined URI, so GetLeftPart(Path) yields "scheme://authority/path" without the query.
        string redactedFull = $"{requestUri.GetLeftPart(UriPartial.Path)}?{RedactedQuery}";

        // Current OpenTelemetry HTTP semantic conventions (.NET 8+).
        if (activity.GetTagItem("url.full") is not null)
        {
            activity.SetTag("url.full", redactedFull);
        }

        if (activity.GetTagItem("url.query") is not null)
        {
            activity.SetTag("url.query", RedactedQuery);
        }

        // Legacy convention emitted by older instrumentation, defended against in case it is ever enabled.
        if (activity.GetTagItem("http.url") is not null)
        {
            activity.SetTag("http.url", redactedFull);
        }
    }
}
