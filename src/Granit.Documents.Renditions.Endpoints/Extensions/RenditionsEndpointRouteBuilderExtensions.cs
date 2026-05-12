using System;
using Granit.Documents.Renditions.Endpoints.Endpoints;
using Granit.Documents.Renditions.Endpoints.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Documents.Renditions.Endpoints.Extensions;

/// <summary>Maps the renditions HTTP surface onto a host route group.</summary>
public static class RenditionsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the rendition endpoints under the supplied <paramref name="group"/> (typically
    /// the same group hosting <c>MapGranitDocuments</c>). Route prefix and OpenAPI tag are
    /// resolved from <see cref="RenditionsEndpointsOptions"/> (bound from configuration
    /// section <c>Documents:Renditions:Endpoints</c>). Permission gating is inherited from
    /// <c>Documents.Documents.Read</c>; no additional rendition-specific permissions are
    /// introduced.
    /// </summary>
    public static RouteGroupBuilder MapGranitDocumentsRenditions(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RenditionsEndpointsOptions options = ((IEndpointRouteBuilder)group)
            .ServiceProvider
            .GetRequiredService<IOptionsMonitor<RenditionsEndpointsOptions>>()
            .CurrentValue;

        group.MapRenditionEndpoints(options);
        return group;
    }
}
