using Granit.Features.Endpoints.Endpoints;
using Granit.Features.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Features.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping feature management endpoints.
/// </summary>
public static class FeaturesEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps feature management endpoints under <c>/{prefix}/features</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="FeaturesEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitFeatures(
        this IEndpointRouteBuilder endpoints,
        Action<FeaturesEndpointsOptions>? configure = null)
    {
        FeaturesEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        group.MapFeaturesReadEndpoints();
        group.MapFeaturesWriteEndpoints();

        return group;
    }
}
