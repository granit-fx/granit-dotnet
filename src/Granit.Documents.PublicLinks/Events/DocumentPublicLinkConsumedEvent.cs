using Granit.Documents.PublicLinks.Domain;
using Granit.Events;

namespace Granit.Documents.PublicLinks.Events;

/// <summary>Raised in-process whenever a public link is successfully redeemed.</summary>
public sealed record DocumentPublicLinkConsumedEvent(
    Guid LinkId,
    Guid? TenantId,
    Guid DocumentId,
    PublicLinkScope Scope,
    int CurrentUses,
    DateTimeOffset ConsumedAt) : IDomainEvent;
