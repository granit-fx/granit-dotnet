using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions.Domain;

namespace Granit.Documents.Renditions;

/// <summary>
/// Storage abstraction for <see cref="DocumentRendition"/> aggregates. The EF Core impl
/// lives in <c>Granit.Documents.Renditions.EntityFrameworkCore</c>; tests can substitute
/// an in-memory implementation.
/// </summary>
public interface IRenditionStore
{
    /// <summary>Lists every rendition attached to the given document version (any status).</summary>
    Task<IReadOnlyList<DocumentRendition>> ListForVersionAsync(
        Guid documentVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every rendition attached to the given document (across versions). Used by
    /// the cascade-on-permanent-delete handler to enumerate the blobs to soft-delete.
    /// </summary>
    Task<IReadOnlyList<DocumentRendition>> ListForDocumentAsync(
        Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a single rendition by its triple key. Returns <c>null</c> when no row
    /// exists yet — the caller decides whether to insert a Pending row.
    /// </summary>
    Task<DocumentRendition?> FindAsync(
        Guid documentVersionId,
        RenditionType type,
        string format,
        CancellationToken cancellationToken = default);

    /// <summary>Persists a new rendition row (typically <see cref="RenditionStatus.Pending"/>).</summary>
    Task AddAsync(DocumentRendition rendition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists state changes on an existing rendition (status transitions captured by
    /// <see cref="DocumentRendition.MarkGenerating"/> / <see cref="DocumentRendition.MarkReady"/>
    /// / <see cref="DocumentRendition.MarkFailed"/>).
    /// </summary>
    Task UpdateAsync(DocumentRendition rendition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every rendition for the given document — invoked on permanent-delete of
    /// the parent <c>Document</c>. Bytes are released by the caller through
    /// <c>IBlobStorage.DeleteAsync</c> before this method runs.
    /// </summary>
    Task DeleteForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
