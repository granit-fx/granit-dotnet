namespace Granit.Documents.Domain;

/// <summary>
/// What a <see cref="DocumentShare"/> is granted on.
/// </summary>
/// <remarks>
/// Persisted as a string (per Granit conventions) and constrained at the database level by the
/// <c>ck_share_target_exactly_one</c> CHECK so that exactly one of <see cref="DocumentShare.FolderId"/>
/// or <see cref="DocumentShare.DocumentId"/> is non-null for any given row.
/// </remarks>
public enum ShareTargetType
{
    /// <summary>Grant applies to a folder (and, when default, propagates via path prefix per ADR-052).</summary>
    Folder,

    /// <summary>Grant applies to a single document.</summary>
    Document,
}
