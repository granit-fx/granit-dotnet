using System.Diagnostics;

namespace Granit.Documents.AssetMetadata.Diagnostics;

/// <summary>Central <see cref="ActivitySource"/> for <c>Granit.Documents.AssetMetadata</c>.</summary>
public static class AssetMetadataActivitySource
{
    /// <summary>The name of the asset-metadata <see cref="ActivitySource"/>.</summary>
    public const string Name = "Granit.Documents.AssetMetadata";

    /// <summary>Singleton <see cref="ActivitySource"/> instance.</summary>
    public static readonly ActivitySource Source = new(Name);

    /// <summary>Span name covering the full pipeline run (every extractor).</summary>
    public const string PipelineExtract = "asset_metadata.pipeline.extract";

    /// <summary>Span name for a single extractor invocation.</summary>
    public const string ExtractorExtract = "asset_metadata.extractor.extract";

    /// <summary>Span name for the GPS scrub on upload.</summary>
    public const string GpsScrub = "asset_metadata.gps_scrub";
}
