using System.Diagnostics;

namespace Granit.IpGeolocation.IpInfo.Internal;

/// <summary>
/// Strips the client IP from the outbound HTTP trace emitted by the built-in <c>System.Net.Http</c>
/// instrumentation.
/// </summary>
/// <remarks>
/// ipinfo.io takes the IP in the request path (<c>/{ip}/json</c>), which the instrumentation records verbatim
/// in the span's <c>url.full</c> / <c>url.path</c> tags — leaking personal data (GDPR) into trace exporters.
/// This handler sits inside the diagnostics handler in the <c>HttpClient</c> pipeline, so
/// <see cref="Activity.Current"/> is the live HTTP-client activity; it overwrites those two tags before the
/// span is exported. The source-name guard ensures it only ever touches the HTTP-client activity, never an
/// ambient inbound (e.g. ASP.NET Core) activity when tracing is otherwise disabled.
/// </remarks>
internal sealed class IpAddressTelemetryRedactionHandler : DelegatingHandler
{
    private const string HttpActivitySourceName = "System.Net.Http";
    private const string RedactedPath = "/redacted";

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

        string redactedFull = requestUri.GetLeftPart(UriPartial.Authority) + RedactedPath;

        // Current OpenTelemetry HTTP semantic conventions (.NET 8+).
        if (activity.GetTagItem("url.full") is not null)
        {
            activity.SetTag("url.full", redactedFull);
        }

        if (activity.GetTagItem("url.path") is not null)
        {
            activity.SetTag("url.path", RedactedPath);
        }

        // Legacy convention emitted by older instrumentation, defended against in case it is ever enabled.
        if (activity.GetTagItem("http.url") is not null)
        {
            activity.SetTag("http.url", redactedFull);
        }
    }
}
