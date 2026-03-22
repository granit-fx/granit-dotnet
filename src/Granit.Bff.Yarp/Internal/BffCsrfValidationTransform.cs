using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;

namespace Granit.Bff.Yarp.Internal;

/// <summary>
/// YARP request transform that validates the <c>X-CSRF-Token</c> header on mutating
/// HTTP methods (POST, PUT, DELETE, PATCH) before proxying the request.
/// Returns 403 Forbidden if the token is missing or invalid.
/// </summary>
internal sealed partial class BffCsrfValidationTransform(
    IBffCsrfTokenGenerator csrfGenerator,
    IOptions<GranitBffOptions> options,
    BffMetrics metrics,
    ILogger<BffCsrfValidationTransform> logger) : RequestTransform
{
    private const string CsrfHeaderName = "X-CSRF-Token";
    private const string RequireAuthMetadataKey = "Granit.Bff.RequireAuth";

    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Delete,
        HttpMethods.Patch,
    };

    public override ValueTask ApplyAsync(RequestTransformContext transformContext)
    {
        HttpContext httpContext = transformContext.HttpContext;

        // Only validate on routes with RequireAuth metadata
        bool requiresAuth = false;
        IReverseProxyFeature? proxyFeature = httpContext.Features.Get<IReverseProxyFeature>();
        if (proxyFeature?.Route.Config.Metadata is { } metadata
            && metadata.TryGetValue(RequireAuthMetadataKey, out string? requireAuthValue))
        {
            requiresAuth = string.Equals(requireAuthValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        if (!requiresAuth)
        {
            return ValueTask.CompletedTask;
        }

        // Only validate mutating methods
        if (!MutatingMethods.Contains(httpContext.Request.Method))
        {
            return ValueTask.CompletedTask;
        }

        GranitBffOptions bffOptions = options.Value;
        string? sessionId = httpContext.Request.Cookies[bffOptions.SessionCookieName];

        if (string.IsNullOrEmpty(sessionId))
        {
            // Token injection transform will handle 401 — skip CSRF validation
            return ValueTask.CompletedTask;
        }

        string? csrfToken = httpContext.Request.Headers[CsrfHeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(csrfToken) || !csrfGenerator.Validate(sessionId, csrfToken))
        {
            LogCsrfRejection(logger, httpContext.Request.Method, httpContext.Request.Path);
            metrics.RecordCsrfRejection(null);
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        }

        return ValueTask.CompletedTask;
    }

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF CSRF validation failed: {Method} {Path}")]
    private static partial void LogCsrfRejection(ILogger logger, string method, string path);
}
