using System;

namespace Granit.Documents.AssetMetadata.Exceptions;

/// <summary>
/// Thrown by <see cref="Pipeline.IAssetMetadataPipeline"/> when a single
/// extractor terminates with an unrecoverable error. The pipeline does not
/// continue past a failing extractor — the caller decides whether to mark the
/// whole row <c>Failed</c> or keep partial results.
/// </summary>
public sealed class AssetMetadataExtractionException : Exception
{
    /// <summary>The extractor that raised the underlying error.</summary>
    public string ExtractorName { get; }

    /// <summary>The source content-type that was being extracted.</summary>
    public string SourceContentType { get; }

    /// <summary>Creates a new <see cref="AssetMetadataExtractionException"/>.</summary>
    public AssetMetadataExtractionException(string extractorName, string sourceContentType, Exception innerException)
        : base($"Asset-metadata extractor '{extractorName}' failed on source '{sourceContentType}'.", innerException)
    {
        ExtractorName = extractorName;
        SourceContentType = sourceContentType;
    }
}
