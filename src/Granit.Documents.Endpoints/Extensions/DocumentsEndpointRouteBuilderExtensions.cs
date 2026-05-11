using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Endpoints;
using Granit.Documents.Endpoints.Folders.Endpoints;
using Granit.Documents.Endpoints.Options;
using Granit.Documents.Endpoints.Quotas.Endpoints;
using Granit.Documents.Endpoints.Shares.Endpoints;
using Granit.QueryEngine.AspNetCore.Extensions;
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
        group.MapShareEndpoints();
        group.MapQuotaEndpoints();

        // Query engine endpoint — paginated, filterable, sortable listing exposed
        // under /documents/query. Front-ends targeting folder-scoped views call
        // `?filter=folderId eq <guid> and status eq Active&sort=name asc`.
        // The tenant filter is applied by the underlying DbContext / IQueryableSource.
        group.MapGranitGroup("documents/query").MapGranitQuery<Document>();

        return group;
    }
}
