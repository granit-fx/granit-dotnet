using Granit.Events;

namespace Granit.Documents.PublicLinks.Events;

/// <summary>Distributed integration event raised when a public link is revoked.</summary>
public sealed record DocumentPublicLinkRevokedEto(
    Guid LinkId,
    Guid? TenantId,
    Guid DocumentId,
    Guid? RevokedBy,
    string? Reason,
    DateTimeOffset RevokedAt) : IIntegrationEvent;
