using Granit.Documents.PublicLinks.Domain;
using Granit.Events;

namespace Granit.Documents.PublicLinks.Events;

/// <summary>Raised in-process when a new <see cref="DocumentPublicLink"/> is created.</summary>
public sealed record DocumentPublicLinkCreatedEvent(
    Guid LinkId,
    Guid? TenantId,
    Guid DocumentId,
    PublicLinkScope Scope,
    DateTimeOffset ExpiresAt,
    int? MaxUses,
    DateTimeOffset CreatedAt) : IDomainEvent;
