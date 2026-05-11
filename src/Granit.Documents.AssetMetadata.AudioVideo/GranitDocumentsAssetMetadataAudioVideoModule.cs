using Granit.Documents.AssetMetadata.AudioVideo.Extensions;
using Granit.Modularity;

namespace Granit.Documents.AssetMetadata.AudioVideo;

/// <summary>
/// Granit module registering <see cref="Internal.AudioVideoMetadataExtractor"/>
/// on top of <c>Granit.Documents.AssetMetadata</c>. Handles every
/// <c>audio/*</c> and <c>video/*</c> source and projects the container /
/// codec / tag fields into the typed columns plus a verbatim raw dump
/// keyed by the <c>audio:</c> or <c>video:</c> prefix.
/// </summary>
[DependsOn(typeof(GranitDocumentsAssetMetadataModule))]
public sealed class GranitDocumentsAssetMetadataAudioVideoModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsAssetMetadataAudioVideo();
    }
}
