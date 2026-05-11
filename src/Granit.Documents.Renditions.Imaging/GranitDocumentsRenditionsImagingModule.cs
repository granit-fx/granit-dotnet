using Granit.Documents.Renditions.Imaging.Extensions;
using Granit.Imaging;
using Granit.Modularity;

namespace Granit.Documents.Renditions.Imaging;

/// <summary>
/// Granit module wiring the <c>image/* → image/*</c> rendition provider (F16.5) plus
/// the synchronous-thumbnail hook subscribed to <c>DocumentVersionAddedEvent</c>.
/// </summary>
/// <remarks>
/// Depends on <see cref="GranitImagingModule"/> for <c>IImageProcessor</c> — hosts pick
/// the backend (<c>Granit.Imaging.MagickNet</c> by default) at composition time.
/// </remarks>
[DependsOn(
    typeof(GranitImagingModule),
    typeof(GranitDocumentsRenditionsModule))]
public sealed class GranitDocumentsRenditionsImagingModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsRenditionsImaging();
    }
}
