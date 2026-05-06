using Granit.Taxonomy.Endpoints.Options;
using Granit.Taxonomy.Endpoints.Tags.Endpoints;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Taxonomy.Endpoints.Extensions;

/// <summary>
/// <see cref="IEndpointRouteBuilder"/> extensions for the Granit.Taxonomy endpoints.
/// </summary>
public static class TaxonomyEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps every Granit.Taxonomy endpoint group under a single root route group.
    /// </summary>
    public static RouteGroupBuilder MapGranitTaxonomy(
        this IEndpointRouteBuilder endpoints,
        Action<TaxonomyEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        TaxonomyEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        if (!string.IsNullOrEmpty(options.RateLimitingPolicy))
        {
            group.RequireRateLimiting(options.RateLimitingPolicy);
        }

        group.MapTagEndpoints();
        group.MapTagAssignmentEndpoints();

        return group;
    }
}
