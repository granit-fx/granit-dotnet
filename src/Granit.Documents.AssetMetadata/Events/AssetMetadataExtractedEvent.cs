using Granit.Events;

namespace Granit.Documents.AssetMetadata.Events;

/// <summary>
/// Raised when a <see cref="Domain.DocumentAssetMetadata"/> row transitions to
/// <see cref="Domain.AssetMetadataStatus.Ready"/>.
/// </summary>
public sealed record AssetMetadataExtractedEvent(
    Guid MetadataId,
    Guid? TenantId,
    Guid DocumentId,
    Guid DocumentVersionId,
    string SourceContentType,
    int ExtractorCount,
    DateTimeOffset ExtractedAt) : IDomainEvent;
