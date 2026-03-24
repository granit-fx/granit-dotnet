using Granit.Events;

namespace Granit.BlobStorage.Events;

/// <summary>
/// Raised when a blob fails validation and transitions to <see cref="BlobStatus.Rejected"/>.
/// </summary>
public sealed record BlobRejectedEvent(
    Guid BlobId,
    string ContainerName,
    string RejectionReason) : IDomainEvent;
