using Granit.Documents.PublicLinks.Domain;
using Granit.Events;

namespace Granit.Documents.PublicLinks.Events;

/// <summary>
/// Distributed integration event for a freshly created public link.
/// The raw token and its hash are deliberately omitted — the bus payload
/// must never carry credential material.
/// </summary>
public sealed record DocumentPublicLinkCreatedEto(
    Guid LinkId,
    Guid? TenantId,
    Guid DocumentId,
    PublicLinkScope Scope,
    DateTimeOffset ExpiresAt,
    int? MaxUses,
    DateTimeOffset CreatedAt) : IIntegrationEvent;
