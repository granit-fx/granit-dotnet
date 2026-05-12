using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.Diagnostics;
using Granit.Documents.AssetMetadata.Exceptions;
using Granit.Documents.AssetMetadata.Extractors;
using Microsoft.Extensions.Logging;

namespace Granit.Documents.AssetMetadata.Pipeline;

/// <summary>
/// Default <see cref="IAssetMetadataPipeline"/> — runs every registered extractor
/// matching the source MIME and surfaces per-extractor failures as
/// <see cref="AssetMetadataExtractionException"/> with the offending extractor's name.
/// </summary>
internal sealed partial class AssetMetadataPipeline(
    IEnumerable<IAssetMetadataExtractor> extractors,
    ILogger<AssetMetadataPipeline> logger) : IAssetMetadataPipeline
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetMetadataResult>> ExtractAsync(
        Stream source,
        string sourceContentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentType);

        using Activity? pipelineSpan = AssetMetadataActivitySource.Source.StartActivity(
            AssetMetadataActivitySource.PipelineExtract);

        List<AssetMetadataResult> results = [];
        foreach (IAssetMetadataExtractor extractor in extractors)
        {
            if (!extractor.CanHandle(sourceContentType))
            {
                continue;
            }

            if (source.CanSeek)
            {
                source.Position = 0;
            }

            using Activity? hopSpan = AssetMetadataActivitySource.Source.StartActivity(
                AssetMetadataActivitySource.ExtractorExtract);
            hopSpan?.SetTag("extractor", extractor.Name);

            try
            {
                AssetMetadataResult result = await extractor
                    .ExtractAsync(source, sourceContentType, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogExtractorFailed(logger, extractor.Name, sourceContentType, ex);
                throw new AssetMetadataExtractionException(extractor.Name, sourceContentType, ex);
            }
        }

        return results;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Asset-metadata extractor {Extractor} failed on source {ContentType}")]
    private static partial void LogExtractorFailed(ILogger logger, string extractor, string contentType, Exception exception);
}
