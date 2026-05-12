using Granit.Documents.Endpoints;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Documents.PublicLinks.Endpoints;

/// <summary>
/// Granit module for the public-links HTTP surface (F18.3). Composes on top of the
/// public-links base module and the parent Documents endpoints so the permission
/// registry and OpenAPI tag conventions are shared.
/// </summary>
[DependsOn(
    typeof(GranitDocumentsPublicLinksModule),
    typeof(GranitDocumentsEndpointsModule))]
public sealed class GranitDocumentsPublicLinksEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsPublicLinksEndpoints();
    }
}
