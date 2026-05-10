using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a trashed <c>Document</c> is permanently deleted (F8.2). The aggregate row
/// stays as a tombstone (<c>Status = PermanentlyDeleted</c>) for the GDPR / ISO 27001
/// audit trail; the bytes have been transitioned to <c>BlobStatus.Deleted</c> via
/// <c>BlobStorage</c>.
/// </summary>
/// <param name="DocumentId">Identifier of the document that was permanently deleted.</param>
/// <param name="TenantId">Identifier of the tenant the document belonged to.</param>
/// <param name="DeletedAt">UTC instant the permanent deletion was recorded.</param>
/// <param name="ReleasedBytes">
/// Sum of <c>DocumentVersion.SizeBytes</c> across the document's active versions, decremented
/// from the tenant's storage quota by the same operation.
/// </param>
public sealed record DocumentPermanentlyDeletedEvent(
    Guid DocumentId,
    Guid? TenantId,
    DateTimeOffset DeletedAt,
    long ReleasedBytes) : IDomainEvent;
