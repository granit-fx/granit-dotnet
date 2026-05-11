using Granit.Events;

namespace Granit.Documents.PublicLinks.Events;

/// <summary>Raised in-process when a public link is revoked by an operator.</summary>
public sealed record DocumentPublicLinkRevokedEvent(
    Guid LinkId,
    Guid? TenantId,
    Guid DocumentId,
    Guid? RevokedBy,
    string? Reason,
    DateTimeOffset RevokedAt) : IDomainEvent;
