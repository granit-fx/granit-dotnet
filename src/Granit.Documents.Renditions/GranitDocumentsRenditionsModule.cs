using Granit.Diagnostics;
using Granit.Documents;
using Granit.Documents.Renditions.Diagnostics;
using Granit.Documents.Renditions.Extensions;
using Granit.Modularity;

namespace Granit.Documents.Renditions;

/// <summary>
/// Granit module for the rendition abstraction layer (F16.1).
/// </summary>
/// <remarks>
/// Anchor module: registers <c>RenditionsMetrics</c> + the
/// <c>Granit.Documents.Renditions</c> <see cref="System.Diagnostics.ActivitySource"/>
/// and the pipeline solver. Provider packages (<c>Granit.Documents.Renditions.Imaging</c>,
/// <c>Granit.Documents.Renditions.Pdf</c>, <c>Granit.Documents.Renditions.Office</c>) and
/// the storage companion (<c>Granit.Documents.Renditions.EntityFrameworkCore</c>) wire
/// themselves on top via their own extension methods.
/// </remarks>
[DependsOn(typeof(GranitDocumentsModule))]
public sealed class GranitDocumentsRenditionsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(RenditionsActivitySource.Name);
        context.Services.AddGranitDocumentsRenditions();
    }
}
