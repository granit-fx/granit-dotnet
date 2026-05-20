using Granit.Http.SecurityHeaders.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.Http.SecurityHeaders.Internal;

/// <summary>
/// Middleware that adds OWASP recommended security response headers.
/// </summary>
/// <remarks>
/// <para>
/// Scalar headers (X-Content-Type-Options, X-Frame-Options, Referrer-Policy,
/// X-XSS-Protection, Permissions-Policy, COOP/COEP/CORP) are applied eagerly
/// at request entry — they do not depend on route matching.
/// </para>
/// <para>
/// The <c>Content-Security-Policy</c> header is applied inside
/// <see cref="HttpResponse.OnStarting(Func{object, Task}, object)"/>, which
/// fires <b>after</b> routing has matched. This lets the composer read
/// <c>context.GetEndpoint()?.Metadata</c> to dispatch the right
/// <see cref="ICspContributor"/>s.
/// </para>
/// <para>
/// All headers are re-applied via <c>OnStarting</c> so they survive
/// <c>Response.Clear()</c> called by <c>ExceptionHandlerMiddleware</c> on
/// unhandled exceptions. <c>OnStarting</c> callbacks are NOT cleared by
/// <c>Response.Clear()</c>.
/// </para>
/// </remarks>
internal sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<GranitSecurityHeadersOptions> _optionsMonitor;
    private readonly CspComposer? _composer;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IOptionsMonitor<GranitSecurityHeadersOptions> optionsMonitor,
        CspComposer? composer = null)
    {
        _next = next;
        _optionsMonitor = optionsMonitor;
        _composer = composer;
    }

    public Task InvokeAsync(HttpContext context)
    {
        GranitSecurityHeadersOptions options = _optionsMonitor.CurrentValue;

        ApplyScalarHeaders(context.Response.Headers, options);

        context.Response.OnStarting(static state =>
        {
            (HttpContext ctx, SecurityHeadersMiddleware self) =
                ((HttpContext, SecurityHeadersMiddleware))state;
            GranitSecurityHeadersOptions opts = self._optionsMonitor.CurrentValue;

            ApplyScalarHeaders(ctx.Response.Headers, opts);
            self.ApplyContentSecurityPolicy(ctx);

            return Task.CompletedTask;
        }, (context, this));

        return _next(context);
    }

    internal static void ApplyScalarHeaders(
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
            headers.XXSSProtection = "0";
        }

        if (options.ReferrerPolicy is { Length: > 0 })
        {
            headers["Referrer-Policy"] = options.ReferrerPolicy;
        }

        if (options.PermissionsPolicy is not null)
        {
            headers["Permissions-Policy"] = options.PermissionsPolicy;
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

    private void ApplyContentSecurityPolicy(HttpContext context)
    {
        if (_composer is null)
        {
            return;
        }

        (string Name, string Value)? composed = _composer.Compose(context);

        // Single source of truth: always remove first. If anything upstream
        // (a [ResponseHeader] attribute, another middleware, an output-cache
        // layer) wrote a CSP, the browser would intersect multiple headers
        // and pick the strictest combination — silently neutralising any
        // contributor relaxation. The framework owns this header.
        context.Response.Headers.Remove("Content-Security-Policy");
        context.Response.Headers.Remove("Content-Security-Policy-Report-Only");

        if (composed is { Value: { Length: > 0 } } c)
        {
            context.Response.Headers[c.Name] = c.Value;
        }
    }
}
