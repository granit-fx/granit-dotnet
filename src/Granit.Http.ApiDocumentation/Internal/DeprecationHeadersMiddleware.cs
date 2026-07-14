using System.Globalization;
using Granit.Http.ApiDocumentation.Deprecation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Granit.Http.ApiDocumentation.Internal;

/// <summary>
/// Emits <c>Deprecation</c>, <c>Sunset</c>, and <c>Link</c> response headers
/// (RFC 8594) when the matched endpoint carries <see cref="DeprecatedAttribute"/>
/// metadata. Auto-registered by <c>AddGranitApiDocumentation</c> through
/// <see cref="DeprecationHeadersStartupFilter"/>, so
/// <c>.WithMetadata(new DeprecatedAttribute { … })</c> works with zero extra wiring.
/// </summary>
/// <remarks>
/// Startup-filter middleware runs before <c>UseRouting</c>, so the endpoint is not
/// resolved yet when this middleware executes. Headers are therefore written from an
/// <see cref="HttpResponse.OnStarting(Func{Task})"/> callback, which fires after
/// routing has populated <c>HttpContext.GetEndpoint()</c>.
/// </remarks>
internal sealed partial class DeprecationHeadersMiddleware(
    RequestDelegate next,
    ILogger<DeprecationHeadersMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            DeprecatedAttribute? metadata = httpContext
                .GetEndpoint()?
                .Metadata
                .GetMetadata<DeprecatedAttribute>();

            if (metadata is not null)
            {
                WriteHeaders(httpContext.Response.Headers, metadata);
            }

            return Task.CompletedTask;
        }, context);

        await next(context).ConfigureAwait(false);

        DeprecatedAttribute? deprecated = context
            .GetEndpoint()?
            .Metadata
            .GetMetadata<DeprecatedAttribute>();

        if (deprecated is not null)
        {
            LogDeprecatedEndpointCalled(logger, context.Request.Path, deprecated.SunsetDate);
        }
    }

    private static void WriteHeaders(IHeaderDictionary headers, DeprecatedAttribute metadata)
    {
        headers["Deprecation"] = "true";

        if (metadata.SunsetDate is { } sunsetDate)
        {
            headers["Sunset"] = sunsetDate
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                .ToString("R", CultureInfo.InvariantCulture);
        }

        if (metadata.Link is not null)
        {
            headers.Link = $"<{metadata.Link}>; rel=\"sunset\"";
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Deprecated endpoint called: {Path} (sunset: {SunsetDate})")]
    private static partial void LogDeprecatedEndpointCalled(
        ILogger logger, string path, DateOnly? sunsetDate);
}
