using Granit.Events;

namespace Granit.BlobStorage.Events;

/// <summary>
/// Raised when a blob upload starts (transitions from Pending to Uploading).
/// Enables OpenTelemetry tracing for the full upload lifecycle.
/// </summary>
/// <param name="BlobId">The unique identifier of the blob descriptor.</param>
/// <param name="ContainerName">Logical container name.</param>
/// <param name="OriginalFileName">Original filename provided by the client.</param>
public sealed record BlobUploadStartedEvent(
    Guid BlobId,
    string ContainerName,
    string OriginalFileName) : IDomainEvent;
