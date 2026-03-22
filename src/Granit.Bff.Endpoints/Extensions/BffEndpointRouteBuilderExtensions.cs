using Granit.Bff.Endpoints.Endpoints;
using Granit.Bff.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Bff.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping BFF endpoints.
/// </summary>
public static class BffEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps BFF authentication endpoints for each configured frontend.
    /// Each frontend gets its own route group under <c>/{pathPrefix}/bff</c> with
    /// login, callback, logout, user claims, and CSRF token generation endpoints.
    /// Also registers static file serving and SPA fallback per frontend.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for further chaining.</returns>
    public static IEndpointRouteBuilder MapGranitBffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        GranitBffOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<GranitBffOptions>>().Value;

        foreach (BffFrontendOptions frontend in options.Frontends)
        {
            MapFrontendEndpoints(endpoints, frontend);
            MapFrontendStaticFiles(endpoints, frontend);
        }

        return endpoints;
    }

    private static void MapFrontendEndpoints(IEndpointRouteBuilder endpoints, BffFrontendOptions frontend)
    {
        string groupPrefix = string.IsNullOrEmpty(frontend.PathPrefix)
            ? "/bff"
            : $"{frontend.PathPrefix}/bff";

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(groupPrefix)
            .WithTags($"BFF ({frontend.Name})");

        group.MapLoginEndpoints(frontend);
        group.MapLogoutEndpoints(frontend);
        group.MapUserEndpoints(frontend);
        group.MapCsrfEndpoints(frontend);
    }

    private static void MapFrontendStaticFiles(IEndpointRouteBuilder endpoints, BffFrontendOptions frontend)
    {
        if (string.IsNullOrEmpty(frontend.StaticFilesPath))
        {
            return;
        }

        string requestPath = string.IsNullOrEmpty(frontend.PathPrefix) ? "" : frontend.PathPrefix;

        if (!Directory.Exists(frontend.StaticFilesPath))
        {
            return;
        }

        PhysicalFileProvider fileProvider = new(Path.GetFullPath(frontend.StaticFilesPath));

        // Register disposal when the application shuts down (file provider is captured by the
        // endpoint lambda and lives for the application lifetime).
        endpoints.ServiceProvider.GetService<IHostApplicationLifetime>()
            ?.ApplicationStopping.Register(fileProvider.Dispose);

        // Serve static files for this frontend
        endpoints.MapGet($"{requestPath}/{{**path}}", IResult (HttpContext context) =>
        {
            string path = context.Request.RouteValues["path"]?.ToString() ?? "index.html";

            // Skip BFF API routes — they are handled by the endpoint group
            if (path.StartsWith("bff/", StringComparison.OrdinalIgnoreCase))
            {
                return TypedResults.NotFound();
            }

            IFileInfo fileInfo = fileProvider.GetFileInfo(path);
            if (fileInfo.Exists && !fileInfo.IsDirectory)
            {
                return TypedResults.Stream(fileInfo.CreateReadStream(), GetContentType(path));
            }

            // SPA fallback: serve index.html for client-side routing
            IFileInfo indexFile = fileProvider.GetFileInfo("index.html");
            if (indexFile.Exists)
            {
                return TypedResults.Stream(indexFile.CreateReadStream(), "text/html");
            }

            return TypedResults.NotFound();
        })
        .WithName($"BffStaticFiles_{frontend.Name}")
        .WithSummary("Serves static files and SPA fallback for the BFF frontend.")
        .WithDescription(
            "Serves static assets from the configured static files path. If the requested "
            + "file is not found, falls back to index.html for client-side routing. "
            + "BFF API routes under /bff/ are skipped and handled by the endpoint group.")
        .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();
    }

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".map" => "application/json",
            _ => "application/octet-stream",
        };
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

        // Match any frontend's /bff/login or /bff/callback path
        if (path.Contains("/bff/login", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/bff/callback", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'";
        }

        await next(context).ConfigureAwait(false);
    }
}
