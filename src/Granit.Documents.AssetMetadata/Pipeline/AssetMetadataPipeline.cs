using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.Diagnostics;
using Granit.Documents.AssetMetadata.Exceptions;
using Granit.Documents.AssetMetadata.Extractors;
using Granit.Documents.AssetMetadata.Internal;
using Granit.Documents.AssetMetadata.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Documents.AssetMetadata.Pipeline;

/// <summary>
/// Default <see cref="IAssetMetadataPipeline"/> — runs every registered extractor
/// matching the source MIME, applies a best-effort per-extractor timeout
/// (<see cref="GranitAssetMetadataOptions.ExtractionTimeout"/>) and the GDPR
/// Art. 5(c) PII-strip pass over each result's <see cref="AssetMetadataResult.RawMetadata"/>
/// before handing it back to the orchestration layer. Per-extractor failures
/// surface as <see cref="AssetMetadataExtractionException"/> with the offending
/// extractor's name; timeouts surface as <see cref="AssetMetadataExtractionTimeoutException"/>.
/// </summary>
internal sealed partial class AssetMetadataPipeline(
    IEnumerable<IAssetMetadataExtractor> extractors,
    AssetMetadataMetrics metrics,
    ILogger<AssetMetadataPipeline> logger,
    IOptions<GranitAssetMetadataOptions>? options = null) : IAssetMetadataPipeline
{
    private readonly GranitAssetMetadataOptions _options = options?.Value ?? new GranitAssetMetadataOptions();

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

            using var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_options.ExtractionTimeout > TimeSpan.Zero)
            {
                linkedCts.CancelAfter(_options.ExtractionTimeout);
            }

            try
            {
                AssetMetadataResult result = await extractor
                    .ExtractAsync(source, sourceContentType, linkedCts.Token)
                    .ConfigureAwait(false);

                result = ApplyPersonalDataStrip(result, sourceContentType);
                results.Add(result);
            }
            catch (OperationCanceledException) when (
                linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                LogExtractorTimedOut(logger, extractor.Name, sourceContentType, _options.ExtractionTimeout);
                metrics.RecordTimeout(tenantId: null, sourceContentType, extractor.Name);
                throw new AssetMetadataExtractionTimeoutException(
                    extractor.Name, sourceContentType, _options.ExtractionTimeout);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogExtractorFailed(logger, extractor.Name, sourceContentType, ex);
                throw new AssetMetadataExtractionException(extractor.Name, sourceContentType, ex);
            }
        }

        return results;
    }

    private AssetMetadataResult ApplyPersonalDataStrip(AssetMetadataResult result, string sourceContentType)
    {
        if (!_options.StripPersonalDataOnUpload)
        {
            return result;
        }

        List<string> hits = PersonalDataKeyMatcher.FindPersonalDataKeys(result.RawMetadata);
        bool hasTypedPii = result.Author is not null
            || result.Artist is not null
            || result.LastModifiedBy is not null;

        if (hits.Count == 0 && !hasTypedPii)
        {
            return result;
        }

        Dictionary<string, string?> trimmed = new(result.RawMetadata.Count, StringComparer.Ordinal);
        foreach ((string key, string? value) in result.RawMetadata)
        {
            if (!PersonalDataKeyMatcher.IsPersonalData(key))
            {
                trimmed[key] = value;
            }
        }

        metrics.RecordPersonalDataStripped(tenantId: null, sourceContentType, hits.Count);

        return result with
        {
            Author = null,
            Artist = null,
            LastModifiedBy = null,
            RawMetadata = trimmed,
        };
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Asset-metadata extractor {Extractor} failed on source {ContentType}")]
    private static partial void LogExtractorFailed(ILogger logger, string extractor, string contentType, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Asset-metadata extractor {Extractor} timed out on source {ContentType} after {Timeout}; orchestration unblocking — the synchronous core may still be running on a thread-pool thread.")]
    private static partial void LogExtractorTimedOut(ILogger logger, string extractor, string contentType, TimeSpan timeout);
}
