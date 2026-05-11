using Granit.Events;

namespace Granit.Documents.AssetMetadata.Events;

/// <summary>
/// Raised when a <see cref="Domain.DocumentAssetMetadata"/> row transitions to
/// <see cref="Domain.AssetMetadataStatus.Failed"/>.
/// </summary>
public sealed record AssetMetadataFailedEvent(
    Guid MetadataId,
    Guid? TenantId,
    Guid DocumentId,
    Guid DocumentVersionId,
    string SourceContentType,
    string Reason,
    DateTimeOffset FailedAt) : IDomainEvent;
