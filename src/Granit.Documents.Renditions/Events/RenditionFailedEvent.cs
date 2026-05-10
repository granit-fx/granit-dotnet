using Granit.Documents.Renditions.Domain;
using Granit.Events;

namespace Granit.Documents.Renditions.Events;

/// <summary>
/// Raised when a <see cref="DocumentRendition"/> transitions to
/// <see cref="RenditionStatus.Failed"/> after the retry budget is exhausted.
/// </summary>
public sealed record RenditionFailedEvent(
    Guid RenditionId,
    Guid? TenantId,
    Guid DocumentId,
    Guid DocumentVersionId,
    RenditionType Type,
    string Format,
    string Reason,
    DateTimeOffset FailedAt) : IDomainEvent;
