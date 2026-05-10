using Granit.BackgroundJobs;
using Granit.Documents.Renditions.BackgroundJobs.Extensions;
using Granit.Modularity;

namespace Granit.Documents.Renditions.BackgroundJobs;

/// <summary>
/// Granit module wiring the F16.4 event-driven generation flow. Subscribes to
/// <c>DocumentVersionAddedEvent</c> and asynchronously generates the rendition set
/// declared by the registered <see cref="IRenditionTypePolicy"/>.
/// </summary>
/// <remarks>
/// The actual pipeline run delegates to <c>IRenditionPipeline</c> registered by
/// <c>Granit.Documents.Renditions</c>; provider packages (Imaging / Pdf / Office) plug
/// in independently. Hosts also need <c>Granit.Documents.Renditions.EntityFrameworkCore</c>
/// to materialise the <see cref="DocumentRendition"/> rows and the rendition blob bytes.
/// </remarks>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitDocumentsRenditionsModule))]
public sealed class GranitDocumentsRenditionsBackgroundJobsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsRenditionsBackgroundJobs();
    }
}
