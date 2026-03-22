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
/// Uses <c>Granit.Bff.Frontend</c> metadata to determine which frontend's session to validate.
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
    private const string FrontendMetadataKey = "Granit.Bff.Frontend";

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
        string? frontendName = null;
        IReverseProxyFeature? proxyFeature = httpContext.Features.Get<IReverseProxyFeature>();
        if (proxyFeature?.Route.Config.Metadata is { } metadata)
        {
            if (metadata.TryGetValue(RequireAuthMetadataKey, out string? requireAuthValue))
            {
                requiresAuth = string.Equals(requireAuthValue, "true", StringComparison.OrdinalIgnoreCase);
            }

            metadata.TryGetValue(FrontendMetadataKey, out frontendName);
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

        // Resolve frontend to get the correct session cookie name
        BffFrontendOptions? frontend = ResolveFrontend(bffOptions, frontendName);
        if (frontend is null)
        {
            // Token injection transform will handle unknown frontend — skip CSRF validation
            return ValueTask.CompletedTask;
        }

        string? sessionId = httpContext.Request.Cookies[frontend.SessionCookieName];

        if (string.IsNullOrEmpty(sessionId))
        {
            // Token injection transform will handle 401 — skip CSRF validation
            return ValueTask.CompletedTask;
        }

        string? csrfToken = httpContext.Request.Headers[CsrfHeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(csrfToken) || !csrfGenerator.Validate(sessionId, csrfToken))
        {
            LogCsrfRejection(logger, httpContext.Request.Method, httpContext.Request.Path, frontend.Name);
            metrics.RecordCsrfRejection(null);
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        }

        return ValueTask.CompletedTask;
    }

    private static BffFrontendOptions? ResolveFrontend(GranitBffOptions options, string? frontendName)
    {
        if (string.IsNullOrEmpty(frontendName))
        {
            // Fall back to the first frontend if only one is configured
            return options.Frontends.Count == 1 ? options.Frontends[0] : null;
        }

        return options.Frontends.Find(f =>
            string.Equals(f.Name, frontendName, StringComparison.OrdinalIgnoreCase));
    }

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF CSRF validation failed: {Method} {Path} for frontend {FrontendName}")]
    private static partial void LogCsrfRejection(ILogger logger, string method, string path, string frontendName);
}
