using Microsoft.AspNetCore.Http;

namespace Granit.Bff.Endpoints.Extensions;

/// <summary>
/// Middleware that adds security headers to BFF login/callback endpoints
/// (<c>X-Frame-Options: DENY</c>, <c>Content-Security-Policy: frame-ancestors 'none'</c>).
/// </summary>
public sealed class BffSecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>Adds X-Frame-Options and CSP headers for BFF auth route paths.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? string.Empty;

        // Match any frontend's /bff/login or /bff/callback path
        if (path.Contains("/bff/login", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/bff/callback", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
        }

        await next(context).ConfigureAwait(false);
    }
}
