using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Granit.Http.ApiVersioning.Deprecation;

/// <summary>
/// Endpoint filter that emits <c>Deprecation</c>, <c>Sunset</c>, and <c>Link</c>
/// response headers when an endpoint is marked with <see cref="DeprecatedAttribute"/>.
/// </summary>
internal sealed partial class DeprecationEndpointFilter(
    ILogger<DeprecationEndpointFilter> logger) : IEndpointFilter
{
    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        DeprecatedAttribute? metadata = context.HttpContext
            .GetEndpoint()?
            .Metadata
            .GetMetadata<DeprecatedAttribute>();

        if (metadata is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        IHeaderDictionary headers = context.HttpContext.Response.Headers;

        headers["Deprecation"] = "true";

        if (metadata.SunsetDate is not null
            && DateTimeOffset.TryParse(metadata.SunsetDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset sunsetDate))
        {
            headers["Sunset"] = sunsetDate.UtcDateTime.ToString("R", CultureInfo.InvariantCulture);
        }

        if (metadata.Link is not null)
        {
            headers.Link = $"<{metadata.Link}>; rel=\"deprecation\"";
        }

        string path = context.HttpContext.Request.Path;
        LogDeprecatedEndpointCalled(logger, path, metadata.SunsetDate);

        return await next(context).ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Deprecated endpoint called: {Path} (sunset: {SunsetDate})")]
    private static partial void LogDeprecatedEndpointCalled(
        ILogger logger, string path, string? sunsetDate);
}
