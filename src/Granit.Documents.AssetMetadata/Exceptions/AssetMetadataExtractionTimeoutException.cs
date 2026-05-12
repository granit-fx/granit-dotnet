using System;

namespace Granit.Documents.AssetMetadata.Exceptions;

/// <summary>
/// Thrown by <see cref="Pipeline.IAssetMetadataPipeline"/> when a single
/// extractor exceeds <c>GranitAssetMetadataOptions.ExtractionTimeout</c>. The
/// timeout is best-effort: the orchestration loop unblocks immediately, but
/// the synchronous core of the underlying NuGet extractor (MetadataExtractor,
/// PdfPig, OpenXml, TagLibSharp) may continue running on a thread-pool thread
/// until it returns naturally.
/// </summary>
public sealed class AssetMetadataExtractionTimeoutException : Exception
{
    /// <summary>The extractor that exceeded the timeout.</summary>
    public string ExtractorName { get; }

    /// <summary>The source content-type that was being extracted.</summary>
    public string SourceContentType { get; }

    /// <summary>The configured per-extractor timeout that elapsed.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Creates a new <see cref="AssetMetadataExtractionTimeoutException"/>.</summary>
    public AssetMetadataExtractionTimeoutException(string extractorName, string sourceContentType, TimeSpan timeout)
        : base($"Asset-metadata extractor '{extractorName}' exceeded the {timeout} timeout on source '{sourceContentType}'.")
    {
        ExtractorName = extractorName;
        SourceContentType = sourceContentType;
        Timeout = timeout;
    }
}
