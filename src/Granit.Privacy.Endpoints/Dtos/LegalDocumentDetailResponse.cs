namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>Detailed view of a legal document version.</summary>
/// <param name="Id">Unique identifier of this document version.</param>
/// <param name="DocumentId">Logical document identifier shared across all versions.</param>
/// <param name="Version">Monotonically increasing version number.</param>
/// <param name="LifecycleStatus">Current lifecycle status (<c>Draft</c>, <c>Published</c>, or <c>Archived</c>).</param>
/// <param name="DisplayName">Human-readable document title.</param>
/// <param name="Description">Optional admin-only changelog note.</param>
/// <param name="TemplateName">Optional Granit.Templating template name for rendered content.</param>
/// <param name="DocumentBlobId">Optional blob ID for a downloadable document.</param>
/// <param name="CreatedAt">UTC timestamp of creation.</param>
/// <param name="LastModifiedAt">UTC timestamp of last modification.</param>
/// <param name="ConcurrencyStamp">Opaque optimistic-concurrency token. Pass back in update requests to detect concurrent modifications (HTTP 409).</param>
public sealed record LegalDocumentDetailResponse(
    Guid Id,
    string DocumentId,
    int Version,
    string LifecycleStatus,
    string DisplayName,
    string? Description,
    string? TemplateName,
    Guid? DocumentBlobId,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt,
    string ConcurrencyStamp);
