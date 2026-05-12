using Granit.Documents.Endpoints;
using Granit.Documents.Renditions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Documents.Renditions.Endpoints;

/// <summary>
/// Granit module for the rendition HTTP surface. Composes on top of the renditions base
/// (<see cref="GranitDocumentsRenditionsModule"/>) and the parent Documents endpoints so
/// the permission registry is shared — renditions inherit
/// <c>Documents.Documents.Read</c> without declaring its own permissions.
/// </summary>
[DependsOn(
    typeof(GranitDocumentsRenditionsModule),
    typeof(GranitDocumentsEndpointsModule))]
public sealed class GranitDocumentsRenditionsEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsRenditionsEndpoints();
    }
}
