using Granit.Http.SecurityHeaders.Internal;
using Granit.Http.SecurityHeaders.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Http.SecurityHeaders.Extensions;

/// <summary>
/// Extension methods for adding Granit security headers middleware to the pipeline.
/// </summary>
public static class SecurityApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit security headers middleware and HSTS to the pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called <b>early</b> in the middleware pipeline (after exception handling,
    /// before routing) to ensure headers are applied to all responses, including errors.
    /// </para>
    /// <para>
    /// Injects the following headers based on <see cref="GranitSecurityHeadersOptions"/>:
    /// <c>X-Content-Type-Options</c>, <c>X-Frame-Options</c>, <c>Referrer-Policy</c>,
    /// <c>Permissions-Policy</c>, <c>X-XSS-Protection</c>, <c>Content-Security-Policy</c>,
    /// <c>Cross-Origin-Opener-Policy</c>, <c>Cross-Origin-Embedder-Policy</c>,
    /// <c>Cross-Origin-Resource-Policy</c>.
    /// </para>
    /// <para>
    /// HSTS (<c>Strict-Transport-Security</c>) is handled by ASP.NET Core's built-in
    /// HSTS middleware, configured with OWASP recommended defaults (1 year, includeSubDomains).
    /// </para>
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseGranitSecurityHeaders(this IApplicationBuilder app)
    {
        GranitSecurityHeadersOptions options = app.ApplicationServices
            .GetRequiredService<IOptions<GranitSecurityHeadersOptions>>().Value;

        if (options.EnableHsts)
        {
            app.UseHsts();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();

        return app;
    }
}
