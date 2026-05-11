using Granit.BackgroundJobs;
using Granit.Documents.AssetMetadata.BackgroundJobs.Extensions;
using Granit.Modularity;

namespace Granit.Documents.AssetMetadata.BackgroundJobs;

/// <summary>
/// Granit module wiring the F17.4 event-driven extraction flow. Subscribes to
/// <c>DocumentVersionAddedEvent</c>, fetches the source bytes via
/// <see cref="IAssetMetadataSourceFetcher"/>, runs the registered extractor
/// chain, and persists the resulting <c>DocumentAssetMetadata</c> row.
/// </summary>
/// <remarks>
/// Extractors (image / PDF / Office / audio / video) plug in independently
/// through their own provider packages. With no extractor registered the
/// pipeline produces an empty result set — the row simply lands
/// <c>Ready</c> with <c>ExtractorCount = 0</c>.
/// </remarks>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitDocumentsAssetMetadataModule))]
public sealed class GranitDocumentsAssetMetadataBackgroundJobsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsAssetMetadataBackgroundJobs();
    }
}
