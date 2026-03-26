using Granit.Http.Security.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.Http.Security.Internal;

/// <summary>
/// Middleware that adds OWASP recommended security response headers.
/// </summary>
internal sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    IOptions<GranitSecurityHeadersOptions> options)
{
    private readonly GranitSecurityHeadersOptions _options = options.Value;

    public Task InvokeAsync(HttpContext context)
    {
        IHeaderDictionary headers = context.Response.Headers;

        if (_options.EnableContentTypeOptions)
        {
            headers.XContentTypeOptions = "nosniff";
        }

        if (_options.XFrameOptions is not null)
        {
            headers.XFrameOptions = _options.XFrameOptions;
        }

        if (_options.DisableXssProtection)
        {
            headers["X-XSS-Protection"] = "0";
        }

        if (_options.ReferrerPolicy is { Length: > 0 })
        {
            headers["Referrer-Policy"] = _options.ReferrerPolicy;
        }

        if (_options.PermissionsPolicy is not null)
        {
            headers["Permissions-Policy"] = _options.PermissionsPolicy;
        }

        if (_options.ContentSecurityPolicy is not null)
        {
            headers.ContentSecurityPolicy = _options.ContentSecurityPolicy;
        }

        if (_options.CrossOriginOpenerPolicy is { Length: > 0 })
        {
            headers["Cross-Origin-Opener-Policy"] = _options.CrossOriginOpenerPolicy;
        }

        if (_options.CrossOriginEmbedderPolicy is not null)
        {
            headers["Cross-Origin-Embedder-Policy"] = _options.CrossOriginEmbedderPolicy;
        }

        if (_options.CrossOriginResourcePolicy is { Length: > 0 })
        {
            headers["Cross-Origin-Resource-Policy"] = _options.CrossOriginResourcePolicy;
        }

        return next(context);
    }
}
