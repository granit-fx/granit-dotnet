using Granit.Bff.Endpoints.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Bff.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping BFF endpoints.
/// </summary>
public static class BffEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps BFF authentication endpoints under <c>/bff</c>: login, callback, logout,
    /// user claims, and CSRF token generation.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitBffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/bff")
            .WithTags("BFF");

        group.MapLoginEndpoints();
        group.MapLogoutEndpoints();
        group.MapUserEndpoints();
        group.MapCsrfEndpoints();

        return group;
    }
}

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

        if (path.StartsWith("/bff/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/bff/callback", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'";
        }

        await next(context).ConfigureAwait(false);
    }
}
