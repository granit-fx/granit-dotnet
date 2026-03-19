using Granit.Core.Events;

namespace Granit.BlobStorage.Events;

/// <summary>
/// Raised when a blob passes all validators and transitions to <see cref="BlobStatus.Valid"/>.
/// </summary>
public sealed record BlobValidatedEvent(
    Guid BlobId,
    string ContainerName,
    string VerifiedContentType,
    long SizeBytes) : IDomainEvent;
