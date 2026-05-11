using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <see cref="Domain.DocumentVersion"/>'s underlying blob is replaced
/// in-place by a GDPR-driven scrub flow (F17.9 — GPS strip on upload). Consumed by
/// <c>Granit.Auditing</c> for the ISO 27001 A.12.4.1 / GDPR Art. 30 trail: every
/// mutation of an otherwise immutable version row leaves an audit entry.
/// </summary>
/// <param name="DocumentId">Document the scrubbed version belongs to.</param>
/// <param name="VersionId">Version whose <c>BlobDescriptorId</c> was replaced.</param>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="OldBlobDescriptorId">Identifier of the original (un-scrubbed) blob, now soft-deleted in BlobStorage.</param>
/// <param name="NewBlobDescriptorId">Identifier of the freshly-uploaded scrubbed blob.</param>
/// <param name="OldSizeBytes">Verified size of the original blob.</param>
/// <param name="NewSizeBytes">Verified size of the scrubbed blob.</param>
/// <param name="Reason">Free-text reason for the scrub (e.g. <c>"gps-strip"</c>).</param>
/// <param name="ScrubbedAt">UTC instant the scrub was finalised.</param>
public sealed record DocumentBlobScrubbedEvent(
    Guid DocumentId,
    Guid VersionId,
    Guid? TenantId,
    Guid OldBlobDescriptorId,
    Guid NewBlobDescriptorId,
    long OldSizeBytes,
    long NewSizeBytes,
    string Reason,
    DateTimeOffset ScrubbedAt) : IDomainEvent;
