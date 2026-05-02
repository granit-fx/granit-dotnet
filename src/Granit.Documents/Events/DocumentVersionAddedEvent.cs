using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a new <c>DocumentVersion</c> is finalised and appended to the parent
/// document's history. Emitted both for the initial v1 (F3.2 finalize flow) and for
/// every subsequent re-upload (F4.1).
/// </summary>
public sealed record DocumentVersionAddedEvent(
    Guid DocumentId,
    Guid? TenantId,
    Guid VersionId,
    int VersionNumber,
    Guid BlobDescriptorId,
    long SizeBytes,
    Guid UploadedByUserId) : IDomainEvent;
