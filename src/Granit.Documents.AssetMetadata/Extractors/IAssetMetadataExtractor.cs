using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Granit.Documents.AssetMetadata.Extractors;

/// <summary>
/// A single extractor responsible for pulling metadata from one MIME family.
/// Multiple extractors can match the same source (e.g. a generic + a specialised
/// one) — the pipeline runs every one that returns <c>true</c> on
/// <see cref="CanHandle"/> and merges the results.
/// </summary>
public interface IAssetMetadataExtractor
{
    /// <summary>Stable provider name used in diagnostics and as the raw-metadata key prefix.</summary>
    string Name { get; }

    /// <summary>Returns <c>true</c> when this extractor wants to inspect <paramref name="sourceContentType"/>.</summary>
    bool CanHandle(string sourceContentType);

    /// <summary>
    /// Pulls metadata from <paramref name="source"/>. Implementations seek
    /// <paramref name="source"/> back to position 0 before consuming so the
    /// pipeline can feed the same stream to the next extractor.
    /// </summary>
    Task<AssetMetadataResult> ExtractAsync(
        Stream source,
        string sourceContentType,
        CancellationToken cancellationToken = default);
}
