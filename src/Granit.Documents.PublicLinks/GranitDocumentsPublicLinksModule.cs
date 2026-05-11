using Granit.Diagnostics;
using Granit.Documents.PublicLinks.Diagnostics;
using Granit.Documents.PublicLinks.Extensions;
using Granit.Modularity;

namespace Granit.Documents.PublicLinks;

/// <summary>
/// Granit module for the public-links abstraction layer (F18.1).
/// </summary>
/// <remarks>
/// Anchor module: registers <see cref="DocumentsPublicLinksMetrics"/>, the
/// <c>Granit.Documents.PublicLinks</c> <see cref="System.Diagnostics.ActivitySource"/>,
/// and the bound <c>GranitDocumentsPublicLinksOptions</c>. Storage
/// (<c>.EntityFrameworkCore</c>), HTTP surface (<c>.Endpoints</c>) and Wolverine
/// handlers wire themselves on top.
/// </remarks>
[DependsOn(typeof(GranitDocumentsModule))]
public sealed class GranitDocumentsPublicLinksModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(DocumentsPublicLinksActivitySource.Name);
        context.Services.AddGranitDocumentsPublicLinks();
    }
}
