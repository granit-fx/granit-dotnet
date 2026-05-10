using System;
using Granit.Documents.Renditions.Endpoints.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.Renditions.Endpoints.Extensions;

/// <summary>Maps the renditions HTTP surface onto a host route group.</summary>
public static class RenditionsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the rendition endpoints under the supplied <paramref name="group"/> (typically
    /// the same group hosting <c>MapGranitDocuments</c>). Permission gating is inherited
    /// from <c>Documents.Documents.Read</c>; no additional rendition-specific permissions
    /// are introduced.
    /// </summary>
    public static RouteGroupBuilder MapGranitDocumentsRenditions(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.MapRenditionEndpoints();
        return group;
    }
}
