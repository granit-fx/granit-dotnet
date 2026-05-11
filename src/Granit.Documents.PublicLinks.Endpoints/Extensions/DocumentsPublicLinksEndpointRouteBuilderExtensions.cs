using Granit.Documents.PublicLinks.Endpoints.Endpoints;
using Granit.Documents.PublicLinks.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.PublicLinks.Endpoints.Extensions;

/// <summary>
/// <see cref="IEndpointRouteBuilder"/> extensions for the public-links endpoints.
/// </summary>
public static class DocumentsPublicLinksEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps both the authenticated admin endpoints (under
    /// <c>{RoutePrefix}/documents/{id}/public-links</c> and <c>/public-links/{id}</c>)
    /// and the anonymous redemption endpoints (under <c>{AnonymousRoutePrefix}/{token}</c>)
    /// onto the supplied <paramref name="endpoints"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder (typically the host application).</param>
    /// <param name="configure">Optional override for <see cref="DocumentsPublicLinksEndpointsOptions"/>.</param>
    public static IEndpointRouteBuilder MapGranitDocumentsPublicLinks(
        this IEndpointRouteBuilder endpoints,
        Action<DocumentsPublicLinksEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        DocumentsPublicLinksEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder adminGroup = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);
        adminGroup.MapAdminEndpoints(options);

        RouteGroupBuilder anonymousGroup = endpoints
            .MapGranitGroup(options.AnonymousRoutePrefix)
            .WithTags(options.TagName);
        anonymousGroup.MapAnonymousEndpoints();

        return endpoints;
    }
}
