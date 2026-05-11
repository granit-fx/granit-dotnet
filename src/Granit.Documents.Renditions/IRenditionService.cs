using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;
using Granit.Documents.Renditions.Domain;

namespace Granit.Documents.Renditions;

/// <summary>
/// Orchestration contract for the rendition HTTP surface (F16.3). Sits between the
/// HTTP layer (<c>Granit.Documents.Renditions.Endpoints</c>) and the storage / pipeline
/// primitives; consumers inject this interface rather than wiring <c>IRenditionStore</c>
/// + <c>IBlobStorage</c> by hand.
/// </summary>
public interface IRenditionService
{
    /// <summary>
    /// Lists every rendition attached to the document's <i>current</i> version, ordered
    /// by <see cref="DocumentRendition.Type"/> then <see cref="DocumentRendition.Format"/>.
    /// Returns <c>null</c> when the document is not found or excluded by the tenant filter.
    /// </summary>
    Task<IReadOnlyList<DocumentRendition>?> ListAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a short-lived presigned download URL for a <see cref="RenditionStatus.Ready"/>
    /// rendition of the document's current version. <paramref name="format"/> is optional —
    /// when omitted the implementation picks the first ready rendition for the requested
    /// <paramref name="type"/> (provider-friendly default; admin UIs typically pass the
    /// explicit MIME). Returns <c>null</c> when:
    /// <list type="bullet">
    ///   <item>the document is not found or excluded by the tenant filter;</item>
    ///   <item>no <see cref="RenditionStatus.Ready"/> row matches.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// On-demand fallback (synchronously running the pipeline for a missing rendition
    /// at download time) is deferred to a follow-up — see EPIC #1781. Hosts can rely on
    /// the F16.4 background job + this read-only path for the first cut.
    /// </remarks>
    Task<PresignedDownloadUrl?> GetDownloadUrlAsync(
        Guid documentId,
        RenditionType type,
        string? format,
        CancellationToken cancellationToken = default);
}
