using Granit.Core.Events;

namespace Granit.Privacy.LegalAgreements.Events;

/// <summary>
/// Published when a user accepts a legal agreement (RGPD Art. 7 — proof of consent).
/// </summary>
/// <remarks>
/// <see cref="LegalAgreements.Domain.LegalAgreementBase"/> is anemic (CreationAuditedEntity) —
/// this event is raised by the store/service after persistence, not by the entity itself.
/// </remarks>
public sealed record LegalAgreementAcceptedEto(
    string UserId,
    string DocumentId,
    string Version,
    DateTimeOffset AcceptedAt) : IIntegrationEvent;
