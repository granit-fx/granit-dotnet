using Microsoft.AspNetCore.Builder;

namespace Granit.Bff.Endpoints.Extensions;

/// <summary>
/// Extension methods for adding BFF middleware to the ASP.NET Core pipeline.
/// </summary>
public static class BffApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the BFF token injection middleware for self-hosted deployments where the API
    /// runs in the same process as the BFF (e.g., colocated OpenIddict + API).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Intercepts requests matching <paramref name="pathPrefix"/>, resolves the BFF frontend
    /// from session cookies, validates CSRF on mutating methods, performs silent token refresh,
    /// and injects an <c>Authorization</c> header. Requests without a BFF session cookie pass
    /// through unmodified.
    /// </para>
    /// <para>
    /// Place this middleware after <c>UseRewriter()</c> (if using path rewriting) and before
    /// <c>UseAuthentication()</c>. For YARP-based deployments, use <c>Granit.Bff.Yarp</c>
    /// instead.
    /// </para>
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="pathPrefix">Request path prefix to intercept. Default: <c>/api</c>.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseGranitBffTokenInjection(
        this IApplicationBuilder app,
        string pathPrefix = "/api")
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<BffTokenInjectionMiddleware>(pathPrefix);
    }
}
