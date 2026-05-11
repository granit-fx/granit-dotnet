using Granit.Documents.PublicLinks.Domain;
using Granit.Events;

namespace Granit.Documents.PublicLinks.Events;

/// <summary>Distributed integration event raised on every successful public-link redemption.</summary>
public sealed record DocumentPublicLinkConsumedEto(
    Guid LinkId,
    Guid? TenantId,
    Guid DocumentId,
    PublicLinkScope Scope,
    int CurrentUses,
    DateTimeOffset ConsumedAt) : IIntegrationEvent;
