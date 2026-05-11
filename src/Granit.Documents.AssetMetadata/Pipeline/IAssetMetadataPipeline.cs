using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Granit.Documents.AssetMetadata.Pipeline;

/// <summary>
/// Orchestrates the registered <c>IAssetMetadataExtractor</c> set: enumerates
/// every extractor advertising <c>CanHandle == true</c>, runs them sequentially
/// against the source bytes, and returns the per-extractor results so the
/// aggregate can merge them.
/// </summary>
public interface IAssetMetadataPipeline
{
    /// <summary>
    /// Runs every applicable extractor and returns its <see cref="AssetMetadataResult"/>.
    /// The list is empty when no extractor matches the source — the caller decides
    /// whether to mark the row Ready (no metadata available) or Failed.
    /// </summary>
    Task<IReadOnlyList<AssetMetadataResult>> ExtractAsync(
        Stream source,
        string sourceContentType,
        CancellationToken cancellationToken = default);
}
