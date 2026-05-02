using Granit.Documents.Domain;

namespace Granit.Documents;

/// <summary>
/// Paged slice of a document's version history along with the parent document's
/// current-version pointer. The latter lets the HTTP mapper flag which row in the
/// page is the active version without re-querying the document aggregate.
/// </summary>
/// <param name="Versions">Page slice ordered by <see cref="DocumentVersion.VersionNumber"/> descending.</param>
/// <param name="TotalCount">Total number of versions for the parent document (across all pages).</param>
/// <param name="CurrentVersionId">Identifier of the parent document's currently-active version.</param>
public sealed record DocumentVersionPage(
    IReadOnlyList<DocumentVersion> Versions,
    int TotalCount,
    Guid? CurrentVersionId);
