using Granit.Documents.Endpoints.Documents.Endpoints;
using Granit.Documents.Endpoints.Folders.Endpoints;
using Granit.Documents.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.Endpoints.Extensions;

/// <summary>
/// <see cref="IEndpointRouteBuilder"/> extensions for the Granit.Documents endpoints.
/// </summary>
public static class DocumentsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps every Granit.Documents endpoint group under a single root route group.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional callback to override <see cref="DocumentsEndpointsOptions"/>.</param>
    /// <returns>The created <see cref="RouteGroupBuilder"/>.</returns>
    public static RouteGroupBuilder MapGranitDocuments(
        this IEndpointRouteBuilder endpoints,
        Action<DocumentsEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        DocumentsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        if (!string.IsNullOrEmpty(options.RateLimitingPolicy))
        {
            group.RequireRateLimiting(options.RateLimitingPolicy);
        }

        group.MapFolderEndpoints();
        group.MapDocumentEndpoints();
        group.MapDocumentTagProxyEndpoints();

        return group;
    }
}
