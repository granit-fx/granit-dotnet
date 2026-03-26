using Granit.Http.SecurityHeaders.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.Http.SecurityHeaders.Internal;

/// <summary>
/// Middleware that adds OWASP recommended security response headers.
/// </summary>
/// <remarks>
/// Headers are applied both directly (for the normal response path) and via
/// <see cref="HttpResponse.OnStarting(Func{object, Task}, object)"/> to survive <c>Response.Clear()</c>
/// called by <c>ExceptionHandlerMiddleware</c> on unhandled exceptions.
/// </remarks>
internal sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    IOptions<GranitSecurityHeadersOptions> options)
{
    private readonly GranitSecurityHeadersOptions _options = options.Value;

    public Task InvokeAsync(HttpContext context)
    {
        ApplyHeaders(context.Response.Headers, _options);

        // Re-apply via OnStarting so headers survive Response.Clear()
        // (called by ExceptionHandlerMiddleware on unhandled exceptions).
        // OnStarting callbacks are NOT cleared by Response.Clear().
        context.Response.OnStarting(static state =>
        {
            (HttpContext ctx, GranitSecurityHeadersOptions opts) = ((HttpContext, GranitSecurityHeadersOptions))state;
            ApplyHeaders(ctx.Response.Headers, opts);
            return Task.CompletedTask;
        }, (context, _options));

        return next(context);
    }

    internal static void ApplyHeaders(
        IHeaderDictionary headers, GranitSecurityHeadersOptions options)
    {
        if (options.EnableContentTypeOptions)
        {
            headers.XContentTypeOptions = "nosniff";
        }

        if (options.XFrameOptions is not null)
        {
            headers.XFrameOptions = options.XFrameOptions;
        }

        if (options.DisableXssProtection)
        {
            headers["X-XSS-Protection"] = "0";
        }

        if (options.ReferrerPolicy is { Length: > 0 })
        {
            headers["Referrer-Policy"] = options.ReferrerPolicy;
        }

        if (options.PermissionsPolicy is not null)
        {
            headers["Permissions-Policy"] = options.PermissionsPolicy;
        }

        if (options.ContentSecurityPolicy is not null)
        {
            headers.ContentSecurityPolicy = options.ContentSecurityPolicy;
        }

        if (options.CrossOriginOpenerPolicy is { Length: > 0 })
        {
            headers["Cross-Origin-Opener-Policy"] = options.CrossOriginOpenerPolicy;
        }

        if (options.CrossOriginEmbedderPolicy is not null)
        {
            headers["Cross-Origin-Embedder-Policy"] = options.CrossOriginEmbedderPolicy;
        }

        if (options.CrossOriginResourcePolicy is { Length: > 0 })
        {
            headers["Cross-Origin-Resource-Policy"] = options.CrossOriginResourcePolicy;
        }
    }
}
