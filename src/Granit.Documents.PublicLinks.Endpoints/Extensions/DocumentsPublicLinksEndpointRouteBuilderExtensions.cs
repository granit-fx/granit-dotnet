using Granit.Documents.PublicLinks.Endpoints.Endpoints;
using Granit.Documents.PublicLinks.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        // Resolve from IOptionsMonitor when the host registered the options through
        // AddGranitDocumentsPublicLinksEndpoints() / configuration binding. Falls
        // back to a fresh default instance when neither monitor nor configure are
        // present (backwards compatible with the original Action<>-only surface).
        IOptionsMonitor<DocumentsPublicLinksEndpointsOptions>? monitor =
            endpoints.ServiceProvider.GetService<IOptionsMonitor<DocumentsPublicLinksEndpointsOptions>>();
        DocumentsPublicLinksEndpointsOptions options = monitor?.CurrentValue ?? new DocumentsPublicLinksEndpointsOptions();
        configure?.Invoke(options);

        RouteGroupBuilder adminGroup = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);
        adminGroup.MapAdminEndpoints(options);

        RouteGroupBuilder anonymousGroup = endpoints
            .MapGranitGroup(options.AnonymousRoutePrefix)
            .WithTags(options.TagName)
            // F18.4 — rate-limit metadata is harmless without UseRateLimiter() / a
            // registered policy: ASP.NET Core treats unknown policies as no-op.
            // Hosts opt in by calling AddGranitDocumentsPublicLinksRateLimiter().
            .RequireRateLimiting(DocumentsPublicLinksRateLimiterServiceCollectionExtensions.PolicyName);
        anonymousGroup.MapAnonymousEndpoints();

        return endpoints;
    }
}
