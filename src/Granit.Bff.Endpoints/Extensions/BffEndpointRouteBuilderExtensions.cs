using System.Runtime.CompilerServices;
using Granit.Bff.Endpoints.Endpoints;
using Granit.Bff.Options;
using Granit.Http.Cookies;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    // Idempotency guard keyed on the route builder instance rather than a process-wide
    // bool, so a second in-process host (e.g. parallel integration tests) maps its own
    // BFF endpoints instead of silently skipping. Entries are collected with the builder.
    private static readonly ConditionalWeakTable<IEndpointRouteBuilder, object> s_mappedBuilders = [];

    /// <summary>
    /// Maps BFF authentication endpoints for each configured frontend.
    /// Each frontend gets its own route group under <c>/{pathPrefix}/bff</c> with
    /// login, callback, logout, user claims, and CSRF token generation endpoints.
    /// Also registers static file serving and SPA fallback per frontend.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for further chaining.</returns>
    public static IEndpointRouteBuilder MapGranitBff(this IEndpointRouteBuilder endpoints)
    {
        if (s_mappedBuilders.TryGetValue(endpoints, out _))
        {
            return endpoints;
        }

        s_mappedBuilders.Add(endpoints, new object());

        GranitBffOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<GranitBffOptions>>().Value;
        ICookieRegistry cookieRegistry = endpoints.ServiceProvider
            .GetRequiredService<ICookieRegistry>();

        foreach (BffFrontendOptions frontend in options.Frontends)
        {
            RegisterBffSessionCookie(cookieRegistry, frontend, options);
            MapFrontendEndpoints(endpoints, frontend);
            MapFrontendStaticFiles(endpoints, frontend);
        }

        return endpoints;
    }

    private static void RegisterBffSessionCookie(
        ICookieRegistry cookieRegistry, BffFrontendOptions frontend, GranitBffOptions bffOptions)
    {
        cookieRegistry.Register(new CookieDefinition(
            Name: frontend.SessionCookieName,
            Category: CookieCategory.StrictlyNecessary,
            RetentionDays: (int)Math.Ceiling(bffOptions.SessionDuration.TotalDays),
            IsHttpOnly: true,
            Purpose: $"BFF session identifier for the '{frontend.Name}' frontend. "
                + "Stores a server-side session ID to associate the browser with stored OIDC tokens.")
        {
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }

    private static void MapFrontendEndpoints(IEndpointRouteBuilder endpoints, BffFrontendOptions frontend)
    {
        string groupPrefix = string.IsNullOrEmpty(frontend.PathPrefix)
            ? "/bff"
            : $"{frontend.PathPrefix}/bff";

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(groupPrefix)
            .WithTags($"BFF - {ToTagSuffix(frontend.Name)}");

        // BFF endpoints are per-session and credential-sensitive (session state, CSRF tokens):
        // they must never enter a shared output cache, or one caller's /bff/user — or worse, a
        // CSRF token — would be served to another, and session state would read stale (login
        // loop / delayed logout). NoCache wins over any host base policy that enables caching;
        // the metadata is inert when output caching is not configured.
        group.CacheOutput(static policy => policy.NoCache());

        group.MapLoginEndpoints(frontend);
        group.MapLogoutEndpoints(frontend);
        group.MapUserEndpoints(frontend);
        group.MapCsrfEndpoints(frontend);
        group.MapBackChannelLogoutEndpoints(frontend);
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
                return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
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

            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        })
        .WithName($"BffStaticFiles_{frontend.Name}")
        .WithSummary("Serves static files and SPA fallback for the BFF frontend.")
        .WithDescription(
            "Serves static assets from the configured static files path. If the requested "
            + "file is not found, falls back to index.html for client-side routing. "
            + "BFF API routes under /bff/ are skipped and handled by the endpoint group.")
        .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription()
        .AllowAnonymous();
    }

    private static string ToTagSuffix(string name) =>
        string.IsNullOrEmpty(name)
            ? name
            : char.ToUpperInvariant(name[0]) + name[1..];

    internal static string GetContentType(string path) =>
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
