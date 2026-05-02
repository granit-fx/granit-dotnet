using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised every time a download URL is issued for a document version. Consumed by
/// <c>Granit.Auditing</c> for the ISO 27001 A.12.4.1 "who downloaded what when" trail.
/// </summary>
/// <param name="DocumentId">Document the download targets.</param>
/// <param name="VersionId">Version the download targets — current at time of request, or the explicitly-requested historical version.</param>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="RequestedByUserId">User who requested the download.</param>
/// <param name="RequestedAt">UTC instant the URL was issued.</param>
/// <param name="UrlExpiresAt">UTC instant the issued URL expires (mirrors the BlobStorage TTL).</param>
public sealed record DocumentDownloadedEvent(
    Guid DocumentId,
    Guid VersionId,
    Guid? TenantId,
    Guid RequestedByUserId,
    DateTimeOffset RequestedAt,
    DateTimeOffset UrlExpiresAt) : IDomainEvent;
