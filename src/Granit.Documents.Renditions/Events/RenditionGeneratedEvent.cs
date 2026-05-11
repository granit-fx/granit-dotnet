using Granit.Documents.Renditions.Domain;
using Granit.Events;

namespace Granit.Documents.Renditions.Events;

/// <summary>Raised when a <see cref="DocumentRendition"/> transitions to <see cref="RenditionStatus.Ready"/>.</summary>
public sealed record RenditionGeneratedEvent(
    Guid RenditionId,
    Guid? TenantId,
    Guid DocumentId,
    Guid DocumentVersionId,
    RenditionType Type,
    string Format,
    long SizeBytes,
    DateTimeOffset GeneratedAt) : IDomainEvent;
